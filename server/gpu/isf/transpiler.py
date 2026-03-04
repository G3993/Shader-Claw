"""ISF GLSL → #version 330 core GLSL transpiler.

Transforms ISF shaders (effectively GLSL ES 1.0 with ISF extensions)
into standard #version 330 core fragment shaders.
"""

from __future__ import annotations
import re
from .types import ISFShader, ISFInput, ISFPass

# Map ISF input types to GLSL uniform types
_TYPE_MAP = {
    "float": "float",
    "bool": "bool",
    "long": "int",
    "point2D": "vec2",
    "color": "vec4",
    "image": "sampler2D",
    "audio": "sampler2D",      # audio waveform texture
    "audioFFT": "sampler2D",   # audio FFT texture
    "event": "bool",
    "text": "float",  # text inputs expand to float uniforms in practice
}

# Types that are textures and need _imgSize companion uniforms
_TEXTURE_TYPES = {"image", "audio", "audioFFT"}


def transpile_isf(shader: ISFShader) -> str:
    """Transpile an ISFShader's GLSL body into a complete #version 330 fragment shader."""
    body = shader.glsl_body
    body = _strip_version(body)
    body = _handle_version_blocks(body)
    body = _replace_varying(body)
    body = _replace_frag_color(body)
    body = _replace_texture2D(body)
    preamble = _build_preamble(shader)
    return preamble + "\n" + body


def _strip_version(body: str) -> str:
    """Remove any existing #version directive."""
    return re.sub(r"^\s*#version\s+\d+.*$", "", body, flags=re.MULTILINE)


def _handle_version_blocks(body: str) -> str:
    """Handle #if __VERSION__ conditional blocks.

    ISF shaders use these to branch between GLSL ES 1.0 and modern GLSL:
      #if __VERSION__ <= 120
        varying vec2 texCoord;
      #else
        in vec2 texCoord;
      #endif

    We keep the #else branch (modern GLSL) and discard the #if branch.
    """
    # Pattern: #if __VERSION__ ... #else ... #endif
    result = []
    lines = body.split("\n")
    in_version_block = False
    in_else_branch = False
    depth = 0

    for line in lines:
        stripped = line.strip()

        if re.match(r"#if\s+__VERSION__", stripped):
            in_version_block = True
            in_else_branch = False
            depth = 1
            continue
        elif in_version_block:
            if stripped == "#else":
                in_else_branch = True
                continue
            elif stripped == "#endif":
                depth -= 1
                if depth == 0:
                    in_version_block = False
                    in_else_branch = False
                    continue
            elif stripped.startswith("#if"):
                depth += 1

            if in_else_branch:
                result.append(line)
            # Skip lines in the #if branch (before #else)
            continue
        else:
            result.append(line)

    return "\n".join(result)


def _replace_varying(body: str) -> str:
    """Replace 'varying' with 'in' for fragment shader inputs."""
    return re.sub(r"\bvarying\b", "in", body)


def _replace_frag_color(body: str) -> str:
    """Replace gl_FragColor with our output variable."""
    return re.sub(r"\bgl_FragColor\b", "_isf_fragColor", body)


def _replace_texture2D(body: str) -> str:
    """Replace texture2D() with texture() for GLSL 330."""
    return re.sub(r"\btexture2D\b", "texture", body)


def _build_preamble(shader: ISFShader) -> str:
    """Build the GLSL preamble: version, outputs, uniforms, helper functions."""
    lines: list[str] = []

    # Version
    lines.append("#version 330")
    lines.append("")

    # Fragment output
    lines.append("out vec4 _isf_fragColor;")
    lines.append("")

    # ISF built-in uniforms
    lines.append("// ISF built-in uniforms")
    lines.append("uniform vec2 RENDERSIZE;")
    lines.append("uniform float TIME;")
    lines.append("uniform float TIMEDELTA;")
    lines.append("uniform int FRAMEINDEX;")
    lines.append("uniform int PASSINDEX;")
    lines.append("uniform vec4 DATE;")  # (year, month, day, seconds-since-midnight)
    lines.append("")

    # Shader-Claw built-in uniforms (audio, mouse, interaction)
    lines.append("// Shader-Claw built-in uniforms")
    lines.append("uniform vec2 mousePos;")
    lines.append("uniform vec2 mouseDelta;")
    lines.append("uniform sampler2D audioFFT;")
    lines.append("uniform float audioLevel;")
    lines.append("uniform float audioBass;")
    lines.append("uniform float audioMid;")
    lines.append("uniform float audioHigh;")
    lines.append("uniform float pinchHold;")
    lines.append("uniform float inputActivity;")
    lines.append("uniform float _voiceGlitch;")
    lines.append("uniform float _transparentBg;")
    lines.append("uniform sampler2D inputImage;")
    lines.append("")

    # Normalized fragment coordinate
    lines.append("// ISF fragment coordinate helpers")
    lines.append("vec2 isf_FragNormCoord = gl_FragCoord.xy / RENDERSIZE;")
    lines.append("")

    # User input uniforms
    if shader.inputs:
        lines.append("// User input uniforms")
        for inp in shader.inputs:
            glsl_type = _TYPE_MAP.get(inp.type, "float")
            lines.append(f"uniform {glsl_type} {inp.name};")
            # Texture inputs also get a _imgSize uniform
            if inp.type in _TEXTURE_TYPES:
                lines.append(f"uniform vec2 _{inp.name}_imgSize;")
        lines.append("")

    # Pass target samplers (multi-pass buffers)
    targets = [p.target for p in shader.passes if p.target]
    if targets:
        lines.append("// Pass target samplers")
        for target in targets:
            lines.append(f"uniform sampler2D {target};")
            lines.append(f"uniform vec2 _{target}_imgSize;")
        lines.append("")

    # ISF texture helper functions
    lines.append("// ISF texture helper functions")
    lines.append("vec4 IMG_NORM_PIXEL(sampler2D tex, vec2 normCoord) {")
    lines.append("    return texture(tex, normCoord);")
    lines.append("}")
    lines.append("")
    lines.append("vec4 IMG_PIXEL(sampler2D tex, vec2 pixelCoord) {")
    lines.append("    vec2 size = textureSize(tex, 0);")
    lines.append("    return texture(tex, pixelCoord / size);")
    lines.append("}")
    lines.append("")
    lines.append("vec4 IMG_THIS_PIXEL(sampler2D tex) {")
    lines.append("    return texture(tex, isf_FragNormCoord);")
    lines.append("}")
    lines.append("")
    lines.append("vec4 IMG_THIS_NORM_PIXEL(sampler2D tex) {")
    lines.append("    return texture(tex, isf_FragNormCoord);")
    lines.append("}")
    lines.append("")
    lines.append("vec2 IMG_SIZE(sampler2D tex) {")
    lines.append("    return vec2(textureSize(tex, 0));")
    lines.append("}")
    lines.append("")

    return "\n".join(lines)
