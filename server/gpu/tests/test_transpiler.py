"""Tests for isf.transpiler — ISF to GLSL 330 transpilation."""

import pytest
from isf.parser import parse_isf_string
from isf.transpiler import transpile_isf


def _transpile(src: str) -> str:
    """Helper: parse ISF source and transpile to GLSL 330."""
    shader = parse_isf_string(src)
    return transpile_isf(shader)


class TestVersionHandling:
    def test_strips_existing_version(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
#version 100
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        # Should have exactly one #version 330
        assert result.count("#version") == 1
        assert "#version 330" in result

    def test_handles_version_conditional(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
#if __VERSION__ <= 120
varying vec2 texOffsets[5];
#else
in vec2 texOffsets[5];
#endif
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        # Should keep the #else branch
        assert "in vec2 texOffsets[5];" in result
        # Should not have 'varying'
        assert "varying" not in result


class TestReplacements:
    def test_replaces_gl_fragcolor(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() {
    gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0);
}'''
        result = _transpile(src)
        assert "gl_FragColor" not in result
        assert "_isf_fragColor = vec4(1.0, 0.0, 0.0, 1.0)" in result

    def test_replaces_varying(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
varying vec2 myCoord;
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "in vec2 myCoord;" in result
        assert "varying" not in result

    def test_replaces_texture2d(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() {
    vec4 c = texture2D(someTex, vec2(0.0));
    gl_FragColor = c;
}'''
        result = _transpile(src)
        assert "texture2D" not in result
        assert "texture(someTex" in result


class TestPreamble:
    def test_has_version(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert result.startswith("#version 330")

    def test_has_output(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "out vec4 _isf_fragColor;" in result

    def test_has_builtin_uniforms(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform vec2 RENDERSIZE;" in result
        assert "uniform float TIME;" in result
        assert "uniform float TIMEDELTA;" in result
        assert "uniform int FRAMEINDEX;" in result
        assert "uniform int PASSINDEX;" in result
        assert "uniform vec4 DATE;" in result

    def test_has_frag_norm_coord(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "isf_FragNormCoord = gl_FragCoord.xy / RENDERSIZE" in result

    def test_float_uniform(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"],
"INPUTS": [{"NAME": "speed", "TYPE": "float", "DEFAULT": 1.0}]}*/
void main() { gl_FragColor = vec4(speed); }'''
        result = _transpile(src)
        assert "uniform float speed;" in result

    def test_bool_uniform(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"],
"INPUTS": [{"NAME": "invert", "TYPE": "bool", "DEFAULT": false}]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform bool invert;" in result

    def test_long_uniform(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"],
"INPUTS": [{"NAME": "mode", "TYPE": "long", "DEFAULT": 0}]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform int mode;" in result

    def test_point2d_uniform(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"],
"INPUTS": [{"NAME": "center", "TYPE": "point2D"}]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform vec2 center;" in result

    def test_color_uniform(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"],
"INPUTS": [{"NAME": "tint", "TYPE": "color"}]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform vec4 tint;" in result

    def test_image_uniform_with_size(self):
        src = '''/*{"DESCRIPTION": "test",
"INPUTS": [{"NAME": "inputImage", "TYPE": "image"}]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform sampler2D inputImage;" in result
        assert "uniform vec2 _inputImage_imgSize;" in result

    def test_pass_target_samplers(self):
        src = '''/*{"DESCRIPTION": "test",
"INPUTS": [{"NAME": "inputImage", "TYPE": "image"}],
"PASSES": [
    {"TARGET": "buf1"},
    {"TARGET": "buf2", "PERSISTENT": true},
    {}
]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "uniform sampler2D buf1;" in result
        assert "uniform sampler2D buf2;" in result
        assert "uniform vec2 _buf1_imgSize;" in result
        assert "uniform vec2 _buf2_imgSize;" in result

    def test_helper_functions(self):
        src = '''/*{"DESCRIPTION": "test", "CATEGORIES": ["Generator"]}*/
void main() { gl_FragColor = vec4(1.0); }'''
        result = _transpile(src)
        assert "vec4 IMG_NORM_PIXEL(sampler2D tex, vec2 normCoord)" in result
        assert "vec4 IMG_PIXEL(sampler2D tex, vec2 pixelCoord)" in result
        assert "vec4 IMG_THIS_PIXEL(sampler2D tex)" in result
        assert "vec4 IMG_THIS_NORM_PIXEL(sampler2D tex)" in result
        assert "vec2 IMG_SIZE(sampler2D tex)" in result


class TestCandyWarp:
    """Test transpilation of CandyWarp — a complex real-world generator."""

    def test_transpiles_candywarp(self):
        from isf.parser import parse_isf_file
        from pathlib import Path
        shader_dir = Path("C:/Users/james/etherea-ai/static/shaders-isf")
        if not shader_dir.exists():
            pytest.skip("Shader directory not found")

        shader = parse_isf_file(shader_dir / "CandyWarp.fs")
        result = transpile_isf(shader)

        # Should have all 9 uniforms
        assert "uniform float scale;" in result
        assert "uniform float cycle;" in result
        assert "uniform float thickness;" in result
        assert "uniform float loops;" in result
        assert "uniform float warp;" in result
        assert "uniform float hue;" in result
        assert "uniform float tint;" in result
        assert "uniform float rate;" in result
        assert "uniform bool invert;" in result

        # gl_FragColor replaced
        assert "gl_FragColor" not in result
        assert "_isf_fragColor" in result

        # Should use RENDERSIZE and TIME
        assert "RENDERSIZE" in result
        assert "TIME" in result
