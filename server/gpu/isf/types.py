"""Data models for ISF shaders."""

from __future__ import annotations
from dataclasses import dataclass, field
from typing import Any


@dataclass
class ISFInput:
    name: str
    type: str  # float, bool, long, point2D, color, image, event, text
    default: Any = None
    min: Any = None
    max: Any = None
    label: str | None = None
    labels: list[str] | None = None  # for 'long' type dropdown
    values: list[int] | None = None  # for 'long' type dropdown


@dataclass
class ISFPass:
    target: str | None = None  # buffer name or None (= screen)
    width: str | None = None  # size expression like "floor($WIDTH/3.0)"
    height: str | None = None
    persistent: bool = False  # retain texture across frames
    description: str | None = None


@dataclass
class ISFShader:
    name: str
    description: str
    inputs: list[ISFInput] = field(default_factory=list)
    passes: list[ISFPass] = field(default_factory=list)
    glsl_body: str = ""
    categories: list[str] = field(default_factory=list)
    credit: str = ""
    is_generator: bool = True


@dataclass
class AudioState:
    bass: float = 0.0
    mid: float = 0.0
    high: float = 0.0
    level: float = 0.0
    beat: float = 0.0
    bpm: float = 120.0
