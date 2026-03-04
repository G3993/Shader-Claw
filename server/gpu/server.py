"""FastAPI HTTP API for the ISF GPU renderer."""

from __future__ import annotations
import asyncio
import io
import json
import queue
import time
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, JSONResponse, Response, StreamingResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
import numpy as np
from PIL import Image

from isf.parser import parse_isf_file, parse_isf_string
from isf.renderer import ISFGPURenderer
from isf.audio import AudioAnalyzer

# ---------------------------------------------------------------------------
# Shared state — initialized by main.py
# ---------------------------------------------------------------------------

renderer: ISFGPURenderer | None = None
audio: AudioAnalyzer | None = None
shader_dir: Path | None = None
library: dict[str, Any] = {}
render_ready: Any = None  # threading.Event, set when renderer is initialized

# Command queue: API threads put commands, render thread executes them
# Commands are (callable, result_future) tuples
cmd_queue: queue.Queue = queue.Queue()

# Latest rendered frame (set by render loop in main.py)
latest_frame: np.ndarray | None = None
latest_frame_jpeg: bytes = b""
frame_count: int = 0
fps: float = 0.0

app = FastAPI(title="ISF GPU Renderer", version="0.1.0")

# Allow cross-origin requests (needed for mirror mode — local browser POSTs to remote pod)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

_static_dir = Path(__file__).parent / "static"
if _static_dir.exists():
    app.mount("/static", StaticFiles(directory=_static_dir), name="static")


# ---------------------------------------------------------------------------
# Request models
# ---------------------------------------------------------------------------

class LoadRequest(BaseModel):
    name: str | None = None
    source: str | None = None


class ParamsRequest(BaseModel):
    model_config = {"extra": "allow"}


class AudioRequest(BaseModel):
    bass: float = 0.0
    mid: float = 0.0
    high: float = 0.0
    level: float = 0.0
    beat: float = 0.0
    bpm: float = 120.0


# ---------------------------------------------------------------------------
# Thread-safe command dispatch
# ---------------------------------------------------------------------------

def _run_on_render_thread(fn):
    """Queue a callable to run on the render thread. Returns the result."""
    result = {}
    event = __import__("threading").Event()

    def wrapper():
        try:
            result["value"] = fn()
        except Exception as e:
            result["error"] = e
        event.set()

    cmd_queue.put(wrapper)
    event.wait(timeout=5.0)
    if "error" in result:
        raise result["error"]
    return result.get("value")


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------

@app.get("/")
async def index():
    """Serve the web interface."""
    return FileResponse(_static_dir / "index.html")


@app.post("/api/isf/load")
async def load_shader(req: LoadRequest):
    """Load a shader by library name or raw source."""
    if renderer is None:
        raise HTTPException(503, "Renderer not initialized")

    try:
        if req.source:
            shader = parse_isf_string(req.source, name=req.name or "inline")
        elif req.name:
            # Look up in library
            if req.name in library and shader_dir:
                path = shader_dir / library[req.name]["file"]
                shader = parse_isf_file(path)
            elif shader_dir:
                # Try direct filename
                candidates = [
                    shader_dir / f"{req.name}.fs",
                    shader_dir / req.name,
                ]
                found = None
                for c in candidates:
                    if c.exists():
                        found = c
                        break
                if found is None:
                    raise HTTPException(404, f"Shader not found: {req.name}")
                shader = parse_isf_file(found)
            else:
                raise HTTPException(400, "No shader directory configured")
        else:
            raise HTTPException(400, "Provide 'name' or 'source'")

        # Load shader on the render thread (GL context lives there)
        _run_on_render_thread(lambda: renderer.load_shader(shader))

        return {
            "status": "ok",
            "name": shader.name,
            "inputs": [
                {
                    "name": i.name, "type": i.type, "default": i.default,
                    "min": i.min, "max": i.max, "label": i.label,
                    "labels": i.labels, "values": i.values,
                }
                for i in shader.inputs
            ],
            "passes": len(shader.passes),
            "is_generator": shader.is_generator,
        }

    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(400, f"Failed to load shader: {e}")


@app.post("/api/isf/params")
async def set_params(req: ParamsRequest):
    """Set uniform values: {"speed": 2.0, "scale": 50}."""
    if renderer is None:
        raise HTTPException(503, "Renderer not initialized")

    params = req.model_dump(exclude_none=True)
    renderer.set_params(params)  # Just dict update, no GL calls — safe from any thread
    return {"status": "ok", "params": params}


@app.post("/api/isf/audio")
async def set_audio(req: AudioRequest):
    """Forward audio features."""
    if audio is None:
        raise HTTPException(503, "Audio not initialized")

    audio.update(
        bass=req.bass, mid=req.mid, high=req.high,
        level=req.level, beat=req.beat, bpm=req.bpm,
    )
    return {"status": "ok"}


@app.get("/api/isf/status")
async def get_status():
    """Current shader, frame count, FPS."""
    shader_name = renderer.shader.name if renderer and renderer.shader else None
    return {
        "shader": shader_name,
        "frame_count": frame_count,
        "fps": round(fps, 1),
        "resolution": f"{renderer.width}x{renderer.height}" if renderer else None,
        "gpu": renderer.gpu_name if renderer else None,
    }


@app.get("/api/isf/frame")
async def get_frame():
    """Latest frame as JPEG."""
    if not latest_frame_jpeg:
        raise HTTPException(503, "No frame available")
    return Response(content=latest_frame_jpeg, media_type="image/jpeg")


@app.get("/api/isf/frame/stream")
async def stream_frames():
    """MJPEG stream for browser preview."""
    def generate():
        while True:
            if latest_frame_jpeg:
                yield (
                    b"--frame\r\n"
                    b"Content-Type: image/jpeg\r\n\r\n"
                    + latest_frame_jpeg
                    + b"\r\n"
                )
            time.sleep(1.0 / 30)  # ~30fps

    return StreamingResponse(
        generate(),
        media_type="multipart/x-mixed-replace; boundary=frame",
    )


@app.get("/api/isf/library")
async def get_library():
    """List available shaders from library."""
    if not library:
        return {"shaders": [], "count": 0}

    shaders = []
    for name, info in library.items():
        shaders.append({
            "name": name,
            "categories": info.get("categories", []),
            "description": info.get("description", ""),
            "is_generator": info.get("is_generator", False),
            "has_audio": info.get("has_audio", False),
            "input_count": len(info.get("inputs", [])),
        })

    return {"shaders": shaders, "count": len(shaders)}


@app.get("/api/isf/frame/rgba")
async def get_frame_rgba():
    """Latest frame as raw RGBA bytes (for NDI / pipeline integration)."""
    if latest_frame is None:
        raise HTTPException(503, "No frame available")
    # latest_frame is (H, W, 3) RGB — add alpha channel for RGBA
    h, w = latest_frame.shape[:2]
    rgba = np.ones((h, w, 4), dtype=np.uint8) * 255
    rgba[:, :, :3] = latest_frame
    return Response(
        content=rgba.tobytes(),
        media_type="application/octet-stream",
        headers={
            "X-Frame-Width": str(w),
            "X-Frame-Height": str(h),
            "X-Frame-Format": "RGBA",
        },
    )


def frame_to_jpeg(frame: np.ndarray, quality: int = 80) -> bytes:
    """Convert numpy RGB frame to JPEG bytes."""
    img = Image.fromarray(frame, "RGB")
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=quality)
    return buf.getvalue()
