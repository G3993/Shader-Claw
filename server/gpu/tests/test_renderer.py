"""Tests for isf.renderer — headless GPU rendering.

These tests require a GPU with OpenGL support.
"""

import pytest
import numpy as np
from pathlib import Path
from isf.parser import parse_isf_file, parse_isf_string
from isf.renderer import ISFGPURenderer

SHADER_DIR = Path("C:/Users/james/etherea-ai/static/shaders-isf")


@pytest.fixture(scope="module")
def renderer():
    """Create a shared renderer for all tests in this module."""
    r = ISFGPURenderer(width=128, height=128)
    yield r
    r.release()


def _simple_generator():
    return parse_isf_string('''/*{
    "DESCRIPTION": "Red shader",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        {"NAME": "brightness", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 1.0}
    ]
}*/
void main() {
    gl_FragColor = vec4(brightness, 0.0, 0.0, 1.0);
}''', name="red")


class TestRendererBasics:
    def test_creates_context(self, renderer):
        assert renderer.gpu_name != "Unknown"
        assert renderer.width == 128
        assert renderer.height == 128

    def test_black_frame_without_shader(self, renderer):
        frame = renderer.render_frame()
        assert frame.shape == (128, 128, 3)
        assert frame.dtype == np.uint8
        assert frame.sum() == 0  # all black

    def test_loads_and_renders(self, renderer):
        shader = _simple_generator()
        renderer.load_shader(shader)
        frame = renderer.render_frame()

        assert frame.shape == (128, 128, 3)
        # Should be mostly red (R > 200, G ~ 0, B ~ 0)
        avg_r = frame[:, :, 0].mean()
        avg_g = frame[:, :, 1].mean()
        avg_b = frame[:, :, 2].mean()
        assert avg_r > 200, f"Expected red > 200, got {avg_r}"
        assert avg_g < 10, f"Expected green < 10, got {avg_g}"
        assert avg_b < 10, f"Expected blue < 10, got {avg_b}"

    def test_param_change(self, renderer):
        shader = _simple_generator()
        renderer.load_shader(shader)

        # Set brightness to 0 → black frame
        renderer.set_params({"brightness": 0.0})
        frame = renderer.render_frame()
        assert frame[:, :, 0].mean() < 10, "Should be dark with brightness=0"

        # Set brightness back to 1 → red frame
        renderer.set_params({"brightness": 1.0})
        frame = renderer.render_frame()
        assert frame[:, :, 0].mean() > 200, "Should be red with brightness=1"

    def test_frame_index_increments(self, renderer):
        shader = _simple_generator()
        renderer.load_shader(shader)
        idx0 = renderer.frame_index
        renderer.render_frame()
        assert renderer.frame_index == idx0 + 1
        renderer.render_frame()
        assert renderer.frame_index == idx0 + 2

    def test_fragment_source_available(self, renderer):
        shader = _simple_generator()
        renderer.load_shader(shader)
        src = renderer.fragment_source
        assert "#version 330" in src
        assert "uniform float brightness;" in src

    def test_render_frame_raw(self, renderer):
        shader = _simple_generator()
        renderer.load_shader(shader)
        renderer.set_params({"brightness": 1.0})
        raw = renderer.render_frame_raw()
        assert isinstance(raw, bytes)
        assert len(raw) == 128 * 128 * 3


@pytest.mark.skipif(not SHADER_DIR.exists(), reason="Shader directory not found")
class TestRealShaders:
    def test_candywarp_renders(self, renderer):
        shader = parse_isf_file(SHADER_DIR / "CandyWarp.fs")
        renderer.load_shader(shader)
        frame = renderer.render_frame()
        assert frame.shape == (128, 128, 3)
        # CandyWarp should produce non-black output
        assert frame.mean() > 5, "CandyWarp should produce visible output"

    def test_candywarp_params(self, renderer):
        shader = parse_isf_file(SHADER_DIR / "CandyWarp.fs")
        renderer.load_shader(shader)

        # Render with default params
        frame1 = renderer.render_frame()

        # Change a param and render again
        renderer.set_params({"scale": 10.0, "hue": 0.5})
        frame2 = renderer.render_frame()

        # Frames should differ (different params + time advancing)
        assert not np.array_equal(frame1, frame2), "Param change should affect output"

    def test_shader_switching(self, renderer):
        """Test loading a different shader after one is already loaded."""
        shader1 = parse_isf_file(SHADER_DIR / "CandyWarp.fs")
        renderer.load_shader(shader1)
        frame1 = renderer.render_frame()

        # Switch to a different shader
        shader2 = _simple_generator()
        renderer.load_shader(shader2)
        frame2 = renderer.render_frame()

        # Should be different (CandyWarp vs solid red)
        assert not np.array_equal(frame1, frame2)


class TestBenchmark:
    """Performance benchmarks — not strict assertions, just informational."""

    def test_render_performance(self, renderer):
        """Render 100 frames and report FPS."""
        shader = _simple_generator()
        renderer.load_shader(shader)

        import time
        t0 = time.perf_counter()
        for _ in range(100):
            renderer.render_frame()
        elapsed = time.perf_counter() - t0

        fps = 100 / elapsed
        ms = elapsed / 100 * 1000
        print(f"\n  Render+readback: {fps:.0f} FPS ({ms:.1f} ms/frame) at {renderer.width}x{renderer.height}")
        # Should at least hit 30 FPS
        assert fps > 30, f"Expected >30 FPS, got {fps:.1f}"
