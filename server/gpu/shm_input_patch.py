#!/usr/bin/env python3
"""
Patch Scope's frame_processor.py to add shared memory input mode.

ShaderClaw writes raw RGBA frames to /workspace/shaderclaw_frame (tmpfs).
This patch adds start_shm_input() / stop_shm_input() / _shm_input_loop()
that reads those frames and feeds them into the pipeline via put().

File format: [width u32 LE][height u32 LE][frame_counter u32 LE][pad u32][RGBA pixels]

Usage: docker exec scope python3 /workspace/shm_input_patch.py
"""

import sys

FP = "/app/src/scope/server/frame_processor.py"

# The code to inject — methods added to FrameProcessor class
SHM_METHODS = '''
    # ─── Shared Memory Input (ShaderClaw) ─────────────────────────────
    # Reads raw RGBA frames from a tmpfs file written by ShaderClaw.
    # File format: [width u32 LE][height u32 LE][counter u32 LE][pad u32][RGBA pixels]

    def start_shm_input(self, shm_path: str = "/workspace/shaderclaw_frame", target_fps: int = 30) -> bool:
        """Start reading video frames from shared memory (tmpfs file).

        Args:
            shm_path: Path to the shared memory frame file
            target_fps: Target frame rate for reading

        Returns:
            True if started successfully
        """
        if getattr(self, '_shm_input_enabled', False):
            logger.warning("SHM input already running, stopping first")
            self.stop_shm_input()

        try:
            # Use pipeline dimensions for RGB conversion
            load_params = getattr(self.pipeline_manager, '_load_params', None) or {}
            self._shm_input_width = load_params.get('width', 1920)
            self._shm_input_height = load_params.get('height', 1080)

            self._shm_input_path = shm_path
            self._shm_input_enabled = True
            self._shm_input_fps = target_fps
            self._shm_input_frame_count = 0
            self._shm_input_last_counter = 0

            # Enable video mode
            self._video_mode = True

            self._shm_input_thread = threading.Thread(
                target=self._shm_input_loop,
                daemon=True,
                name="shm_input"
            )
            self._shm_input_thread.start()

            logger.info(f"SHM input started from {shm_path} at {target_fps} FPS")
            return True

        except Exception as e:
            logger.error(f"Failed to start SHM input: {e}")
            self._shm_input_enabled = False
            return False

    def stop_shm_input(self):
        """Stop reading from shared memory."""
        was_enabled = getattr(self, '_shm_input_enabled', False)
        self._shm_input_enabled = False

        if hasattr(self, '_shm_input_thread') and self._shm_input_thread:
            self._shm_input_thread.join(timeout=2)
            self._shm_input_thread = None

        if was_enabled:
            count = getattr(self, '_shm_input_frame_count', 0)
            logger.info(f"SHM input stopped after {count} frames")

    def _shm_input_loop(self):
        """Background thread that reads frames from tmpfs and feeds to put()."""
        import struct

        interval = 1.0 / self._shm_input_fps
        header_size = 16  # 4 u32s
        pipeline_w = self._shm_input_width
        pipeline_h = self._shm_input_height

        logger.info(f"SHM input loop started, pipeline res: {pipeline_w}x{pipeline_h}")

        while self._shm_input_enabled and self.running:
            try:
                with open(self._shm_input_path, 'rb') as f:
                    header = f.read(header_size)
                    if len(header) < header_size:
                        time.sleep(interval)
                        continue

                    width, height, counter, _ = struct.unpack('<IIII', header)

                    # Skip if same frame as last time
                    if counter == self._shm_input_last_counter:
                        time.sleep(0.001)  # Brief sleep, check again soon
                        continue

                    self._shm_input_last_counter = counter

                    # Read RGBA pixel data
                    expected_size = width * height * 4
                    rgba_data = f.read(expected_size)
                    if len(rgba_data) != expected_size:
                        time.sleep(interval)
                        continue

                # Convert RGBA to RGB numpy array
                frame_rgba = np.frombuffer(rgba_data, dtype=np.uint8).reshape((height, width, 4))
                frame_rgb = frame_rgba[:, :, :3]  # Drop alpha channel

                # Resize to pipeline resolution if needed
                if width != pipeline_w or height != pipeline_h:
                    from PIL import Image
                    img = Image.fromarray(frame_rgb)
                    img = img.resize((pipeline_w, pipeline_h), Image.BILINEAR)
                    frame_rgb = np.array(img)

                # Create VideoFrame and feed to processor
                video_frame = VideoFrame.from_ndarray(frame_rgb, format='rgb24')
                self.put(video_frame)

                self._shm_input_frame_count += 1

                if self._shm_input_frame_count % 300 == 0:
                    logger.info(f"SHM input: {self._shm_input_frame_count} frames, "
                                f"src={width}x{height}, pipeline={pipeline_w}x{pipeline_h}")

                time.sleep(interval)

            except FileNotFoundError:
                time.sleep(0.5)  # File not yet created, wait
            except Exception as e:
                logger.error(f"SHM input error: {e}")
                time.sleep(interval)

        logger.info(f"SHM input loop ended after {self._shm_input_frame_count} frames")
        self._shm_input_enabled = False
'''

# Also add the HTTP API endpoint for controlling SHM input
SHM_API_HANDLER = '''
        elif self.path == '/input/shm':
            # Start/stop shared memory input from ShaderClaw
            content_length = int(self.headers.get('Content-Length', 0))
            body = self.rfile.read(content_length).decode() if content_length else '{}'
            try:
                data = json.loads(body)
            except json.JSONDecodeError:
                self._send_json(400, {'error': 'Invalid JSON'})
                return

            path = data.get('path', '/workspace/shaderclaw_frame')
            if data.get('stop'):
                fp.stop_shm_input()
                self._send_json(200, {'success': True, 'message': 'SHM input stopped'})
            else:
                fps = data.get('fps', 30)
                success = fp.start_shm_input(shm_path=path, target_fps=fps)
                if success:
                    logger.info(f"SHM input started via API: {path}")
                    self._send_json(200, {'success': True, 'message': f'SHM input started from {path}'})
                else:
                    self._send_json(500, {'error': 'Failed to start SHM input'})
'''

def main():
    with open(FP) as f:
        src = f.read()

    if "start_shm_input" in src:
        print("SHM input already patched")
        return 0

    # 1. Inject SHM methods into FrameProcessor class
    # Find the end of stop_rtmp_input or get_rtmp_input_status to insert after
    marker = "def get_rtmp_input_status(self) -> dict:"
    if marker not in src:
        print(f"ERROR: Could not find '{marker}' in frame_processor.py")
        return 1

    # Find the end of get_rtmp_input_status method (next def or end of indented block)
    idx = src.index(marker)
    # Find the next method definition at same indentation level
    next_def = src.find("\n    def ", idx + len(marker))
    if next_def < 0:
        print("ERROR: Could not find insertion point after get_rtmp_input_status")
        return 1

    src = src[:next_def] + "\n" + SHM_METHODS + src[next_def:]

    # 2. Inject HTTP API handler for /input/shm
    # Insert before the final 'else' in do_POST
    api_marker = "        elif self.path == '/input/rtmp':"
    if api_marker in src:
        # Find the end of the /input/rtmp handler block and insert after it
        rtmp_idx = src.index(api_marker)
        # Find the next elif or else after this block
        next_handler = src.find("\n        else:", rtmp_idx + len(api_marker))
        if next_handler > 0:
            src = src[:next_handler] + "\n" + SHM_API_HANDLER + src[next_handler:]
            print("SHM API handler injected")
        else:
            print("WARNING: Could not inject SHM API handler (no else: found)")
    else:
        print("WARNING: Could not find /input/rtmp handler, SHM API handler not injected")

    with open(FP, "w") as f:
        f.write(src)

    print("SHM input patch applied successfully")
    return 0

if __name__ == "__main__":
    sys.exit(main())
