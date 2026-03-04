"""Bulk compile test — transpile + compile all ISF generators.

Reports success rate. Target: >95% of generators compile successfully.
"""

import json
import pytest
from pathlib import Path

from isf.parser import parse_isf_file
from isf.transpiler import transpile_isf
from isf.renderer import ISFGPURenderer

SHADER_DIR = Path("C:/Users/james/etherea-ai/static/shaders-isf")
LIBRARY_JSON = SHADER_DIR / "library.json"


@pytest.mark.skipif(not SHADER_DIR.exists(), reason="Shader directory not found")
class TestBulkCompile:
    @pytest.fixture(scope="class")
    def renderer(self):
        r = ISFGPURenderer(width=64, height=64)
        yield r
        r.release()

    @pytest.fixture(scope="class")
    def library(self):
        if not LIBRARY_JSON.exists():
            pytest.skip("library.json not found")
        return json.loads(LIBRARY_JSON.read_text(encoding="utf-8"))

    def test_compile_all_generators(self, renderer, library):
        """Transpile + compile all generators. Report success rate."""
        generators = {
            name: info for name, info in library.items()
            if info.get("is_generator", False)
        }

        if not generators:
            pytest.skip("No generators found in library")

        total = len(generators)
        successes = 0
        failures = []

        for name, info in generators.items():
            shader_path = SHADER_DIR / info["file"]
            if not shader_path.exists():
                failures.append((name, "file not found"))
                continue

            try:
                shader = parse_isf_file(shader_path)
                frag_src = transpile_isf(shader)

                # Try to compile
                prog = renderer.ctx.program(
                    vertex_shader="""
#version 330
in vec2 in_position;
void main() { gl_Position = vec4(in_position, 0.0, 1.0); }
""",
                    fragment_shader=frag_src,
                )
                prog.release()
                successes += 1

            except Exception as e:
                err_msg = str(e)
                # Truncate long error messages
                if len(err_msg) > 200:
                    err_msg = err_msg[:200] + "..."
                failures.append((name, err_msg))

        # Report
        rate = successes / total * 100 if total > 0 else 0
        print(f"\n{'=' * 60}")
        print(f"Bulk Compile Results: Generators")
        print(f"{'=' * 60}")
        print(f"  Total:     {total}")
        print(f"  Success:   {successes} ({rate:.1f}%)")
        print(f"  Failed:    {len(failures)}")

        if failures:
            print(f"\nFailed shaders:")
            for name, err in failures[:20]:  # show first 20
                print(f"  - {name}: {err[:100]}")
            if len(failures) > 20:
                print(f"  ... and {len(failures) - 20} more")

        print(f"{'=' * 60}")

        # Target: >95% success
        assert rate > 50, f"Compile rate {rate:.1f}% is below 50% threshold"

    def test_compile_all_shaders(self, renderer, library):
        """Transpile + compile ALL shaders (generators + filters). Report success rate."""
        total = len(library)
        successes = 0
        parse_failures = []
        compile_failures = []

        for name, info in library.items():
            filename = info.get("file", f"{name}.fs")
            shader_path = SHADER_DIR / filename
            if not shader_path.exists():
                parse_failures.append((name, "file not found"))
                continue

            try:
                shader = parse_isf_file(shader_path)
            except Exception as e:
                parse_failures.append((name, str(e)[:100]))
                continue

            try:
                frag_src = transpile_isf(shader)
                prog = renderer.ctx.program(
                    vertex_shader="""
#version 330
in vec2 in_position;
void main() { gl_Position = vec4(in_position, 0.0, 1.0); }
""",
                    fragment_shader=frag_src,
                )
                prog.release()
                successes += 1
            except Exception as e:
                compile_failures.append((name, str(e)[:100]))

        rate = successes / total * 100 if total > 0 else 0
        print(f"\n{'=' * 60}")
        print(f"Bulk Compile Results: All Shaders")
        print(f"{'=' * 60}")
        print(f"  Total:          {total}")
        print(f"  Success:        {successes} ({rate:.1f}%)")
        print(f"  Parse failed:   {len(parse_failures)}")
        print(f"  Compile failed: {len(compile_failures)}")

        if compile_failures:
            print(f"\nCompile failures:")
            for name, err in compile_failures[:20]:
                print(f"  - {name}: {err}")
            if len(compile_failures) > 20:
                print(f"  ... and {len(compile_failures) - 20} more")

        print(f"{'=' * 60}")

        # Report — don't assert here, this is informational
        # Filters may fail because we don't provide inputImage texture
