"""Headless GPU renderer for ISF shaders using ModernGL.

Supports multi-pass rendering with persistent textures, dynamic shader
loading, and numpy frame output.
"""

from __future__ import annotations
import math
import os
import struct
import time
from typing import Any

import moderngl
import numpy as np

from .types import ISFShader, ISFPass, AudioState
from .transpiler import transpile_isf
from .audio import AudioAnalyzer

# Force NVIDIA GPU on Windows (Optimus laptops)
if os.name == "nt":
    import ctypes
    try:
        ctypes.windll.nvapi64.NvAPI_Initialize()
    except Exception:
        pass

VERTEX_SHADER = """
#version 330
in vec2 in_position;
void main() {
    gl_Position = vec4(in_position, 0.0, 1.0);
}
"""

# Fullscreen quad (triangle strip)
_QUAD_DATA = struct.pack("8f", -1, -1, 1, -1, -1, 1, 1, 1)


def _eval_size_expr(expr: str | None, width: int, height: int) -> int | None:
    """Evaluate ISF size expression like 'floor($WIDTH/3.0)'."""
    if expr is None:
        return None
    s = expr.replace("$WIDTH", str(width)).replace("$HEIGHT", str(height))
    try:
        return int(eval(s, {"__builtins__": {}}, {"floor": math.floor, "ceil": math.ceil}))
    except Exception:
        return None


class _PassState:
    """State for a single render pass: FBO + texture."""

    def __init__(self, ctx: moderngl.Context, width: int, height: int, persistent: bool):
        self.width = width
        self.height = height
        self.persistent = persistent
        self.texture = ctx.texture((width, height), 4)
        self.texture.filter = (moderngl.LINEAR, moderngl.LINEAR)
        self.fbo = ctx.framebuffer(color_attachments=[self.texture])

    def release(self):
        self.fbo.release()
        self.texture.release()


class ISFGPURenderer:
    """Headless ISF shader renderer.

    Usage:
        renderer = ISFGPURenderer(576, 320)
        renderer.load_shader(shader)
        frame = renderer.render_frame()  # np.ndarray (H, W, 3) uint8 RGB
    """

    def __init__(self, width: int = 576, height: int = 320):
        self.width = width
        self.height = height

        # Create standalone OpenGL context (no window needed)
        # On headless Linux (no DISPLAY), use EGL backend explicitly
        kwargs = {"standalone": True}
        if os.name != "nt" and not os.environ.get("DISPLAY"):
            kwargs["backend"] = "egl"
        self.ctx = moderngl.create_context(**kwargs)
        self.gpu_name = self.ctx.info.get("GL_RENDERER", "Unknown")

        # Quad VBO (shared across all shaders)
        self._quad_buf = self.ctx.buffer(data=_QUAD_DATA)

        # Output FBO for final pass
        self._out_texture = self.ctx.texture((width, height), 4)
        self._out_texture.filter = (moderngl.NEAREST, moderngl.NEAREST)
        self._out_fbo = self.ctx.framebuffer(color_attachments=[self._out_texture])

        # Current shader state
        self._shader: ISFShader | None = None
        self._program: moderngl.Program | None = None
        self._vao: moderngl.VertexArray | None = None
        self._pass_states: list[_PassState] = []
        self._frag_src: str = ""

        # Uniforms
        self._params: dict[str, Any] = {}
        self._audio = AudioAnalyzer()

        # Time
        self._start_time = time.perf_counter()
        self._frame_index = 0
        self._last_frame_time = 0.0

    @property
    def shader(self) -> ISFShader | None:
        return self._shader

    @property
    def frame_index(self) -> int:
        return self._frame_index

    @property
    def fragment_source(self) -> str:
        return self._frag_src

    def load_shader(self, shader: ISFShader) -> None:
        """Load and compile an ISF shader. Releases previous shader resources."""
        self._cleanup_shader()

        self._shader = shader
        self._frag_src = transpile_isf(shader)

        # Compile
        self._program = self.ctx.program(
            vertex_shader=VERTEX_SHADER,
            fragment_shader=self._frag_src,
        )
        self._vao = self.ctx.simple_vertex_array(self._program, self._quad_buf, "in_position")

        # Set up multi-pass FBOs
        self._setup_passes(shader.passes)

        # Set default uniform values
        for inp in shader.inputs:
            if inp.default is not None:
                self._params[inp.name] = inp.default

        # Reset time
        self._start_time = time.perf_counter()
        self._frame_index = 0

    def _setup_passes(self, passes: list[ISFPass]) -> None:
        """Create FBOs for each pass that has a TARGET."""
        for p in passes:
            if p.target is None:
                continue  # final pass renders to output FBO
            w = _eval_size_expr(p.width, self.width, self.height) or self.width
            h = _eval_size_expr(p.height, self.width, self.height) or self.height
            self._pass_states.append(_PassState(self.ctx, w, h, p.persistent))

    def set_params(self, params: dict[str, Any]) -> None:
        """Set shader uniform values."""
        self._params.update(params)

    def set_audio(self, **kwargs) -> None:
        """Update audio state."""
        self._audio.update(**kwargs)

    def render_frame(self) -> np.ndarray:
        """Render one frame and return as numpy array (H, W, 3) uint8 RGB."""
        if self._program is None:
            # No shader loaded — return black frame
            return np.zeros((self.height, self.width, 3), dtype=np.uint8)

        t = time.perf_counter() - self._start_time
        dt = t - self._last_frame_time
        self._last_frame_time = t

        passes = self._shader.passes if self._shader else []

        # If no passes defined, or only passes with no target, single-pass render
        if not self._pass_states:
            self._render_single_pass(t, dt)
        else:
            self._render_multi_pass(t, dt, passes)

        self._frame_index += 1

        # Read pixels from output FBO
        raw = self._out_fbo.read(components=3)
        frame = np.frombuffer(raw, dtype=np.uint8).reshape(self.height, self.width, 3)
        # OpenGL renders bottom-up, flip to top-down
        return np.flipud(frame)

    def render_frame_raw(self) -> bytes:
        """Render one frame and return raw RGB bytes (no numpy copy)."""
        if self._program is None:
            return b"\x00" * (self.width * self.height * 3)

        t = time.perf_counter() - self._start_time
        dt = t - self._last_frame_time
        self._last_frame_time = t

        passes = self._shader.passes if self._shader else []
        if not self._pass_states:
            self._render_single_pass(t, dt)
        else:
            self._render_multi_pass(t, dt, passes)

        self._frame_index += 1
        return self._out_fbo.read(components=3)

    def _render_single_pass(self, t: float, dt: float) -> None:
        """Render a single-pass shader to the output FBO."""
        self._out_fbo.use()
        self.ctx.viewport = (0, 0, self.width, self.height)
        self.ctx.clear(0.0, 0.0, 0.0)
        self._bind_uniforms(t, dt, pass_index=0)
        self._vao.render(moderngl.TRIANGLE_STRIP)

    def _render_multi_pass(self, t: float, dt: float, passes: list[ISFPass]) -> None:
        """Render a multi-pass shader."""
        # Build pass_state index: maps TARGET name → _PassState
        target_states: dict[str, _PassState] = {}
        state_idx = 0
        for p in passes:
            if p.target:
                target_states[p.target] = self._pass_states[state_idx]
                state_idx += 1

        # Render each pass
        for pass_idx, p in enumerate(passes):
            if p.target and p.target in target_states:
                # Render to this pass's FBO
                ps = target_states[p.target]
                ps.fbo.use()
                self.ctx.viewport = (0, 0, ps.width, ps.height)
                if not ps.persistent:
                    self.ctx.clear(0.0, 0.0, 0.0)
            else:
                # Final pass — render to output FBO
                self._out_fbo.use()
                self.ctx.viewport = (0, 0, self.width, self.height)
                self.ctx.clear(0.0, 0.0, 0.0)

            # Bind pass target textures as samplers
            tex_unit = 0
            for target_name, ps in target_states.items():
                if target_name in self._program:
                    ps.texture.use(location=tex_unit)
                    self._program[target_name].value = tex_unit
                    tex_unit += 1
                # Also bind _imgSize for this target
                size_name = f"_{target_name}_imgSize"
                if size_name in self._program:
                    self._program[size_name].value = (float(ps.width), float(ps.height))

            self._bind_uniforms(t, dt, pass_index=pass_idx)
            self._vao.render(moderngl.TRIANGLE_STRIP)

    def _bind_uniforms(self, t: float, dt: float, pass_index: int) -> None:
        """Set all uniforms on the current program."""
        prog = self._program

        # Built-in ISF uniforms
        if "RENDERSIZE" in prog:
            prog["RENDERSIZE"].value = (float(self.width), float(self.height))
        if "TIME" in prog:
            prog["TIME"].value = t
        if "TIMEDELTA" in prog:
            prog["TIMEDELTA"].value = dt
        if "FRAMEINDEX" in prog:
            prog["FRAMEINDEX"].value = self._frame_index
        if "PASSINDEX" in prog:
            prog["PASSINDEX"].value = pass_index
        if "DATE" in prog:
            now = time.localtime()
            secs = now.tm_hour * 3600 + now.tm_min * 60 + now.tm_sec
            prog["DATE"].value = (float(now.tm_year), float(now.tm_mon), float(now.tm_mday), float(secs))

        # User input uniforms
        for name, value in self._params.items():
            if name not in prog:
                continue
            uniform = prog[name]
            try:
                if isinstance(value, bool):
                    uniform.value = value
                elif isinstance(value, (int, float)):
                    uniform.value = value
                elif isinstance(value, (list, tuple)):
                    uniform.value = tuple(float(v) for v in value)
            except Exception:
                pass  # skip incompatible values

        # Audio uniforms
        audio = self._audio.get_uniforms()
        for name, value in audio.items():
            if name in prog:
                prog[name].value = value

    def _cleanup_shader(self) -> None:
        """Release GPU resources for the current shader."""
        for ps in self._pass_states:
            ps.release()
        self._pass_states.clear()

        if self._vao is not None:
            self._vao.release()
            self._vao = None
        if self._program is not None:
            self._program.release()
            self._program = None

        self._params.clear()
        self._shader = None
        self._frag_src = ""

    def release(self) -> None:
        """Release all GPU resources."""
        self._cleanup_shader()
        self._out_fbo.release()
        self._out_texture.release()
        self._quad_buf.release()
        self.ctx.release()
