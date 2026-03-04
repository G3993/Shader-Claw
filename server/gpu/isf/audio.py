"""Audio state container — accepts forwarded float values from external audio analysis."""

from __future__ import annotations
import time
from .types import AudioState


class AudioAnalyzer:
    """Holds audio state forwarded from an external source (browser, FFT service, etc.).

    Not doing real audio analysis here — just a container that accepts
    pre-computed audio features and makes them available as uniforms.
    """

    def __init__(self):
        self.state = AudioState()
        self._last_update = 0.0

    def update(self, **kwargs) -> None:
        """Update audio state from forwarded values."""
        for key in ("bass", "mid", "high", "level", "beat", "bpm"):
            if key in kwargs:
                setattr(self.state, key, float(kwargs[key]))
        self._last_update = time.monotonic()

    @property
    def age(self) -> float:
        """Seconds since last audio update."""
        if self._last_update == 0.0:
            return float("inf")
        return time.monotonic() - self._last_update

    def get_uniforms(self) -> dict[str, float]:
        """Return audio values as a flat dict suitable for uniform binding."""
        # Decay to zero if no update in 2 seconds
        if self.age > 2.0:
            return {
                "audioBass": 0.0, "audioMid": 0.0, "audioHigh": 0.0,
                "audioLevel": 0.0, "beat": 0.0, "bpm": 120.0,
            }
        return {
            "audioBass": self.state.bass,
            "audioMid": self.state.mid,
            "audioHigh": self.state.high,
            "audioLevel": self.state.level,
            "beat": self.state.beat,
            "bpm": self.state.bpm,
        }
