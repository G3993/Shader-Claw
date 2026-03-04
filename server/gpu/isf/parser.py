"""ISF file parser — extract JSON header + GLSL body from .fs files."""

from __future__ import annotations
import json
import re
from pathlib import Path
from .types import ISFShader, ISFInput, ISFPass


def _extract_header_and_body(text: str) -> tuple[dict, str]:
    """Extract the JSON header dict and GLSL body from ISF source text.

    Supports two formats:
      /*{...}*/   (JSON directly after /*)
      /*\\n{...}\\n*/  (JSON with surrounding whitespace)
    """
    # Try /*{ first (most common)
    start = text.find("/*{")
    if start != -1:
        end = text.find("}*/", start)
        if end != -1:
            json_str = text[start + 2 : end + 1]  # skip /*, include }
            body = text[end + 3 :].strip()
            return json.loads(json_str), body

    # Fallback: /* ... */ where inner content is JSON
    start = text.find("/*")
    if start == -1:
        raise ValueError("No ISF header found (no /* block)")
    end = text.find("*/", start + 2)
    if end == -1:
        raise ValueError("No ISF header found (unclosed /* block)")
    json_str = text[start + 2 : end].strip()
    body = text[end + 2 :].strip()
    return json.loads(json_str), body


def _parse_input(raw: dict) -> ISFInput:
    """Parse a single INPUTS entry into an ISFInput."""
    return ISFInput(
        name=raw["NAME"],
        type=raw["TYPE"],
        default=raw.get("DEFAULT"),
        min=raw.get("MIN"),
        max=raw.get("MAX"),
        label=raw.get("LABEL"),
        labels=raw.get("LABELS"),
        values=raw.get("VALUES"),
    )


def _parse_pass(raw: dict) -> ISFPass:
    """Parse a single PASSES entry into an ISFPass."""
    return ISFPass(
        target=raw.get("TARGET"),
        width=raw.get("WIDTH"),
        height=raw.get("HEIGHT"),
        persistent=raw.get("PERSISTENT", False),
        description=raw.get("DESCRIPTION"),
    )


def _has_image_input(inputs: list[ISFInput]) -> bool:
    return any(inp.type == "image" for inp in inputs)


def _build_shader(name: str, meta: dict, glsl_body: str) -> ISFShader:
    inputs = [_parse_input(raw) for raw in meta.get("INPUTS", [])]
    passes = [_parse_pass(raw) for raw in meta.get("PASSES", [])]
    categories = meta.get("CATEGORIES", [])
    is_gen = "Generator" in categories or not _has_image_input(inputs)

    return ISFShader(
        name=name,
        description=meta.get("DESCRIPTION", ""),
        inputs=inputs,
        passes=passes,
        glsl_body=glsl_body,
        categories=categories,
        credit=meta.get("CREDIT", ""),
        is_generator=is_gen,
    )


def parse_isf_file(path: str | Path) -> ISFShader:
    """Parse an ISF .fs file from disk."""
    p = Path(path)
    text = p.read_text(encoding="utf-8", errors="replace")
    meta, body = _extract_header_and_body(text)
    return _build_shader(p.stem, meta, body)


def parse_isf_string(source: str, name: str = "inline") -> ISFShader:
    """Parse ISF source from a string (e.g. from Firestore or API)."""
    meta, body = _extract_header_and_body(source)
    return _build_shader(name, meta, body)
