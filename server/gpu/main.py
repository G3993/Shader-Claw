"""ISF GPU Renderer — headless rendering backend for Shader-Claw.

Starts the headless renderer in a background thread and serves the
FastAPI HTTP API. Deployable standalone or alongside LongLive on a pod.

Config via environment variables:
    SHADER_DIR     — path to ISF shader directory (default: ../../shaders)
    LIBRARY_JSON   — path to library.json (default: SHADER_DIR/library.json)
    DEFAULT_SHADER — shader to load on startup (default: none)
    WIDTH          — render width (default: 576)
    HEIGHT         — render height (default: 320)
    TARGET_FPS     — render FPS target (default: 30)
    PORT           — HTTP server port (default: 8002)
"""

from __future__ import annotations
import json
import os
import sys
import threading
import time
from pathlib import Path

import uvicorn

from isf.parser import parse_isf_file
from isf.renderer import ISFGPURenderer
from isf.audio import AudioAnalyzer
import server

# ---------------------------------------------------------------------------
# Config (env vars with sensible defaults)
# ---------------------------------------------------------------------------

_HERE = Path(__file__).parent

# Default shader dir: Shader-Claw's own shaders/ directory
SHADER_DIR = Path(os.environ.get("SHADER_DIR", _HERE.parent.parent / "shaders"))
LIBRARY_JSON = Path(os.environ.get("LIBRARY_JSON", SHADER_DIR / "library.json"))
DEFAULT_SHADER = os.environ.get("DEFAULT_SHADER", "")
WIDTH = int(os.environ.get("WIDTH", "576"))
HEIGHT = int(os.environ.get("HEIGHT", "320"))
TARGET_FPS = int(os.environ.get("TARGET_FPS", "30"))
PORT = int(os.environ.get("PORT", "8002"))


# ---------------------------------------------------------------------------
# Render loop
# ---------------------------------------------------------------------------

def render_loop(target_fps: int = 30):
    """Background render loop — creates GL context on this thread and renders."""
    # Create renderer ON this thread so the OpenGL context is bound here
    renderer = ISFGPURenderer(width=WIDTH, height=HEIGHT)
    print(f"GPU: {renderer.gpu_name}")
    print(f"Resolution: {WIDTH}x{HEIGHT}")

    # Load default shader
    if DEFAULT_SHADER and SHADER_DIR.exists():
        try:
            shader_file = SHADER_DIR / f"{DEFAULT_SHADER}.fs"
            if shader_file.exists():
                shader = parse_isf_file(shader_file)
                renderer.load_shader(shader)
                print(f"Loaded default shader: {DEFAULT_SHADER}")
            else:
                print(f"Default shader not found: {shader_file}")
        except Exception as e:
            print(f"Failed to load default shader: {e}")

    # Wire renderer to server (API endpoints use this)
    server.renderer = renderer

    frame_time = 1.0 / target_fps
    frame_count = 0
    fps_start = time.perf_counter()

    print(f"Render loop started at {target_fps} FPS target")
    server.render_ready.set()

    while True:
        t0 = time.perf_counter()

        # Process any queued commands from API threads (shader loads etc.)
        while not server.cmd_queue.empty():
            try:
                cmd = server.cmd_queue.get_nowait()
                cmd()
            except Exception as e:
                print(f"Command error: {e}")

        try:
            frame = renderer.render_frame()
            server.latest_frame = frame
            server.latest_frame_jpeg = server.frame_to_jpeg(frame)
            server.frame_count += 1
            frame_count += 1
        except Exception as e:
            print(f"Render error: {e}")
            time.sleep(0.1)
            continue

        # FPS calculation
        now = time.perf_counter()
        elapsed = now - fps_start
        if elapsed >= 1.0:
            server.fps = frame_count / elapsed
            frame_count = 0
            fps_start = now

        # Frame timing
        render_time = time.perf_counter() - t0
        sleep_time = frame_time - render_time
        if sleep_time > 0:
            time.sleep(sleep_time)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    print("=" * 60)
    print("ISF GPU Renderer (Shader-Claw)")
    print("=" * 60)

    # Create audio analyzer
    audio = AudioAnalyzer()

    # Load shader library
    library = {}
    if LIBRARY_JSON.exists():
        library = json.loads(LIBRARY_JSON.read_text(encoding="utf-8"))
        print(f"Loaded shader library: {len(library)} shaders")
    else:
        # No library.json — scan shader dir for .fs files
        if SHADER_DIR.exists():
            for fs in SHADER_DIR.glob("*.fs"):
                library[fs.stem] = {"file": fs.name, "categories": [], "is_generator": True}
            if library:
                print(f"Scanned shader directory: {len(library)} shaders")
            else:
                print(f"No shaders found in {SHADER_DIR}")
        else:
            print(f"Shader directory not found: {SHADER_DIR}")

    # Wire up server state (renderer wired inside render_loop thread)
    server.audio = audio
    server.shader_dir = SHADER_DIR
    server.library = library
    server.render_ready = threading.Event()

    # Start render loop in background thread — GL context created there
    render_thread = threading.Thread(
        target=render_loop,
        args=(TARGET_FPS,),
        daemon=True,
    )
    render_thread.start()

    # Wait for renderer to be ready before starting API
    server.render_ready.wait(timeout=10)

    # Start FastAPI server
    print(f"\nAPI server starting on http://localhost:{PORT}")
    print(f"  Web UI:        http://localhost:{PORT}/")
    print(f"  MJPEG preview: http://localhost:{PORT}/api/isf/frame/stream")
    print(f"  Status:        http://localhost:{PORT}/api/isf/status")
    print(f"  Library:       http://localhost:{PORT}/api/isf/library")
    print("=" * 60)

    uvicorn.run(server.app, host="0.0.0.0", port=PORT, log_level="warning")


if __name__ == "__main__":
    main()
