"""Tests for isf.parser — ISF file parsing."""

import pytest
from pathlib import Path
from isf.parser import parse_isf_file, parse_isf_string

SHADER_DIR = Path("C:/Users/james/etherea-ai/static/shaders-isf")


class TestParseISFString:
    def test_simple_generator(self):
        src = '''/*{
    "DESCRIPTION": "Test shader",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        {"NAME": "speed", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 5.0}
    ]
}*/

void main() {
    gl_FragColor = vec4(1.0);
}'''
        shader = parse_isf_string(src, name="test")
        assert shader.name == "test"
        assert shader.description == "Test shader"
        assert len(shader.inputs) == 1
        assert shader.inputs[0].name == "speed"
        assert shader.inputs[0].type == "float"
        assert shader.inputs[0].default == 1.0
        assert shader.inputs[0].min == 0.0
        assert shader.inputs[0].max == 5.0
        assert shader.is_generator is True
        assert "void main()" in shader.glsl_body

    def test_filter_with_image_input(self):
        src = '''/*{
    "DESCRIPTION": "A filter",
    "CATEGORIES": ["Blur"],
    "INPUTS": [
        {"NAME": "inputImage", "TYPE": "image"},
        {"NAME": "amount", "TYPE": "float", "DEFAULT": 1.0}
    ]
}*/
void main() { gl_FragColor = vec4(0.0); }'''
        shader = parse_isf_string(src)
        assert shader.is_generator is False
        assert len(shader.inputs) == 2
        assert shader.inputs[0].type == "image"

    def test_multi_pass(self):
        src = '''/*{
    "DESCRIPTION": "Multi-pass",
    "INPUTS": [{"NAME": "inputImage", "TYPE": "image"}],
    "PASSES": [
        {"TARGET": "buf1", "WIDTH": "floor($WIDTH/2.0)", "HEIGHT": "floor($HEIGHT/2.0)"},
        {"TARGET": "buf2", "PERSISTENT": true},
        {}
    ]
}*/
void main() { gl_FragColor = vec4(0.0); }'''
        shader = parse_isf_string(src)
        assert len(shader.passes) == 3
        assert shader.passes[0].target == "buf1"
        assert shader.passes[0].width == "floor($WIDTH/2.0)"
        assert shader.passes[1].persistent is True
        assert shader.passes[2].target is None

    def test_bool_input(self):
        src = '''/*{
    "DESCRIPTION": "Bool test",
    "CATEGORIES": ["Generator"],
    "INPUTS": [{"NAME": "invert", "TYPE": "bool", "DEFAULT": false}]
}*/
void main() { gl_FragColor = vec4(0.0); }'''
        shader = parse_isf_string(src)
        assert shader.inputs[0].type == "bool"
        assert shader.inputs[0].default is False

    def test_long_input_with_labels(self):
        src = '''/*{
    "DESCRIPTION": "Long test",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        {"NAME": "mode", "TYPE": "long", "DEFAULT": 0,
         "LABELS": ["Add", "Multiply"], "VALUES": [0, 1]}
    ]
}*/
void main() { gl_FragColor = vec4(0.0); }'''
        shader = parse_isf_string(src)
        assert shader.inputs[0].type == "long"
        assert shader.inputs[0].labels == ["Add", "Multiply"]
        assert shader.inputs[0].values == [0, 1]

    def test_point2d_input(self):
        src = '''/*{
    "DESCRIPTION": "Point2D test",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        {"NAME": "center", "TYPE": "point2D", "DEFAULT": [0.5, 0.5],
         "MIN": [0, 0], "MAX": [1, 1]}
    ]
}*/
void main() { gl_FragColor = vec4(0.0); }'''
        shader = parse_isf_string(src)
        assert shader.inputs[0].type == "point2D"
        assert shader.inputs[0].default == [0.5, 0.5]

    def test_no_inputs(self):
        src = '''/*{
    "DESCRIPTION": "Bare shader",
    "CATEGORIES": ["Generator"]
}*/
void main() { gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0); }'''
        shader = parse_isf_string(src)
        assert len(shader.inputs) == 0
        assert shader.is_generator is True

    def test_malformed_header_raises(self):
        with pytest.raises(Exception):
            parse_isf_string("no header here\nvoid main() {}")

    def test_whitespace_format(self):
        """Test the /* \\n{...}\\n */ format."""
        src = '''/*
{
    "DESCRIPTION": "Whitespace format",
    "CATEGORIES": ["Generator"],
    "INPUTS": []
}
*/
void main() { gl_FragColor = vec4(1.0); }'''
        shader = parse_isf_string(src)
        assert shader.description == "Whitespace format"


@pytest.mark.skipif(not SHADER_DIR.exists(), reason="Shader directory not found")
class TestParseRealShaders:
    def test_candywarp(self):
        shader = parse_isf_file(SHADER_DIR / "CandyWarp.fs")
        assert shader.name == "CandyWarp"
        # CandyWarp has 8 float + 1 bool = 9 inputs
        assert len(shader.inputs) == 9
        types = [i.type for i in shader.inputs]
        assert types.count("float") == 8
        assert types.count("bool") == 1
        assert shader.is_generator is True

    def test_multi_pass_gaussian(self):
        shader = parse_isf_file(SHADER_DIR / "Multi Pass Gaussian Blur.fs")
        assert len(shader.passes) == 11
        assert shader.passes[0].target == "halfSizeBaseRender"
        assert shader.passes[0].width == 'floor($WIDTH/3.0)'
        assert shader.is_generator is False  # requires inputImage

    def test_optical_flow_generator(self):
        shader = parse_isf_file(SHADER_DIR / "Optical Flow Generator.fs")
        assert any(p.persistent for p in shader.passes)
        # Has 3 passes, one persistent
        targets = [p.target for p in shader.passes if p.target]
        assert "delayBuffer" in targets
        assert "maskBuffer" in targets
