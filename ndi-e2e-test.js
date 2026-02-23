// End-to-end NDI test: WebSocket → server → NDI out → NDI receive → verify
// No browser needed.
import WebSocket from "ws";
import grandi from "grandi";

const PORT = 7777;
const WIDTH = 320;
const HEIGHT = 240;
const TEST_FRAMES = 30;

function log(msg) { console.log(`[test] ${msg}`); }
function fail(msg) { console.error(`FAIL: ${msg}`); process.exit(1); }

// Step 1: Connect WebSocket to server
log("Connecting WebSocket...");
const ws = new WebSocket(`ws://localhost:${PORT}`);
ws.binaryType = "arraybuffer";

let msgId = -1000;
const pending = new Map();

function request(action, params = {}) {
  return new Promise((resolve, reject) => {
    const id = msgId--;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error("timeout")); }, 5000);
    pending.set(id, { resolve, reject, timer });
    ws.send(JSON.stringify({ id, action, params }));
  });
}

ws.on("message", (data) => {
  if (data instanceof ArrayBuffer) return; // skip binary
  try {
    const msg = JSON.parse(data.toString());
    const entry = pending.get(msg.id);
    if (entry) {
      clearTimeout(entry.timer);
      pending.delete(msg.id);
      if (msg.error) entry.reject(new Error(msg.error));
      else entry.resolve(msg.result);
    }
  } catch {}
});

await new Promise((resolve, reject) => {
  ws.on("open", resolve);
  ws.on("error", () => reject(new Error("WebSocket connect failed")));
});
log("WebSocket connected.");

// Step 2: Start NDI sender on server
log("Starting NDI sender...");
const sendResult = await request("ndi_send_start", { name: "ShaderClaw E2E Test", width: WIDTH, height: HEIGHT });
log(`NDI sender started: ${JSON.stringify(sendResult)}`);

// Step 3: Send synthetic binary frames (red gradient)
log(`Sending ${TEST_FRAMES} test frames via WebSocket...`);
let sentCount = 0;

function sendTestFrame(frameNum) {
  const pixels = new Uint8Array(WIDTH * HEIGHT * 4);
  for (let y = 0; y < HEIGHT; y++) {
    for (let x = 0; x < WIDTH; x++) {
      const i = (y * WIDTH + x) * 4;
      pixels[i]     = Math.round((x / WIDTH) * 255);           // R: gradient
      pixels[i + 1] = Math.round((y / HEIGHT) * 255);          // G: gradient
      pixels[i + 2] = Math.round(((frameNum / TEST_FRAMES) * 255)); // B: changes per frame
      pixels[i + 3] = 255;                                     // A
    }
  }

  const header = new ArrayBuffer(9);
  const view = new DataView(header);
  view.setUint8(0, 0x02); // FRAME_TYPE_CANVAS
  view.setUint32(1, WIDTH, true);
  view.setUint32(5, HEIGHT, true);

  const msg = new Uint8Array(9 + pixels.length);
  msg.set(new Uint8Array(header), 0);
  msg.set(pixels, 9);
  ws.send(msg.buffer);
  sentCount++;
}

// Send frames at ~30fps
for (let i = 0; i < TEST_FRAMES; i++) {
  sendTestFrame(i);
  await new Promise(r => setTimeout(r, 33));
}
log(`Sent ${sentCount} frames.`);

// Step 4: Discover and receive from NDI
log("Discovering NDI sources...");
const finder = await grandi.find({ showLocalSources: true });
let source = null;
for (let i = 0; i < 20; i++) {
  const sources = finder.sources();
  source = sources.find(s => s.name.includes("E2E Test"));
  if (source) break;
  await new Promise(r => setTimeout(r, 500));
}

if (!source) {
  // Try finding any ShaderClaw source
  const sources = finder.sources();
  log(`Available sources: ${sources.map(s => s.name).join(", ") || "(none)"}`);
  fail("Could not find NDI source 'ShaderClaw E2E Test'");
}

log(`Found source: ${source.name}`);
const receiver = await grandi.receive({
  source,
  colorFormat: grandi.COLOR_FORMAT_RGBX_RGBA,
});

// Keep sending frames while we try to receive
const sendInterval = setInterval(() => sendTestFrame(sentCount), 33);

let receivedCount = 0;
let receivedNonBlack = 0;
log("Receiving NDI frames...");

for (let attempt = 0; attempt < 60; attempt++) {
  try {
    const frame = await receiver.video(500);
    if (frame && frame.data) {
      receivedCount++;
      const d = Buffer.from(frame.data);
      let nonBlack = 0;
      for (let j = 0; j < Math.min(d.length, 4000); j += 4) {
        if (d[j] > 0 || d[j + 1] > 0 || d[j + 2] > 0) nonBlack++;
      }
      log(`  Received frame ${receivedCount}: ${frame.xres}x${frame.yres}, ${nonBlack}/1000 non-black`);
      if (nonBlack > 0) receivedNonBlack++;
      if (receivedCount >= 10) break;
    }
  } catch {
    // timeout
  }
}

clearInterval(sendInterval);

// Step 5: Cleanup
log("Cleaning up...");
await request("ndi_send_stop").catch(() => {});
receiver.destroy();
finder.destroy();
ws.close();

// Step 6: Report
console.log("\n=== NDI E2E Test Results ===");
console.log(`Frames sent via WebSocket: ${sentCount}`);
console.log(`Frames received via NDI:   ${receivedCount}`);
console.log(`Non-black frames:          ${receivedNonBlack}`);
console.log(`Status: ${receivedCount > 0 && receivedNonBlack > 0 ? "PASS" : "FAIL"}`);

process.exit(receivedCount > 0 && receivedNonBlack > 0 ? 0 : 1);
