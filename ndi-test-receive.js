// Receives from an NDI source and reports frame stats
import grandi from "grandi";

const TARGET = process.argv[2] || "ShaderClaw";

console.log(`Looking for NDI source matching "${TARGET}"...`);
const finder = await grandi.find({ showLocalSources: true });

// Poll for sources (finder needs time to discover)
let source = null;
for (let i = 0; i < 20; i++) {
  const sources = finder.sources();
  console.log(`  Found ${sources.length} source(s):`, sources.map(s => s.name).join(", ") || "(none)");
  source = sources.find(s => s.name.includes(TARGET));
  if (source) break;
  await new Promise(r => setTimeout(r, 500));
}

if (!source) {
  console.log("ERROR: Source not found after 10s. Is the NDI broadcast active?");
  finder.destroy();
  process.exit(1);
}

console.log(`\nConnecting to: ${source.name} (${source.urlAddress})`);
const receiver = await grandi.receive({
  source,
  colorFormat: grandi.COLOR_FORMAT_RGBX_RGBA,
  allowVideoFields: false,
});

let frameCount = 0;
let firstFrame = null;
let lastFrame = null;
const startTime = Date.now();
const maxFrames = 30;

console.log(`Receiving up to ${maxFrames} frames...\n`);

for (let attempt = 0; attempt < maxFrames + 50; attempt++) {
  try {
    const frame = await receiver.video(500);
    if (frame && frame.data) {
      frameCount++;
      const info = {
        frame: frameCount,
        xres: frame.xres,
        yres: frame.yres,
        dataBytes: frame.data.length,
        expectedBytes: frame.xres * frame.yres * 4,
      };

      // Sample some pixels to check they're not all black
      const d = Buffer.from(frame.data);
      let nonBlack = 0;
      for (let i = 0; i < Math.min(d.length, 4000); i += 4) {
        if (d[i] > 0 || d[i + 1] > 0 || d[i + 2] > 0) nonBlack++;
      }
      info.sampleNonBlackPixels = nonBlack;
      info.sampleTotal = Math.min(d.length / 4, 1000);

      if (!firstFrame) firstFrame = info;
      lastFrame = info;

      process.stdout.write(`  Frame ${frameCount}: ${frame.xres}x${frame.yres}, ${nonBlack}/${info.sampleTotal} non-black pixels\n`);

      if (frameCount >= maxFrames) break;
    }
  } catch (e) {
    // Timeout — no frame this interval
  }
}

const elapsed = (Date.now() - startTime) / 1000;
console.log(`\n--- Results ---`);
console.log(`Frames received: ${frameCount} in ${elapsed.toFixed(1)}s (${(frameCount / elapsed).toFixed(1)} fps)`);
if (firstFrame) {
  console.log(`Resolution: ${firstFrame.xres}x${firstFrame.yres}`);
  console.log(`Data size: ${firstFrame.dataBytes} bytes (expected ${firstFrame.expectedBytes})`);
  console.log(`Non-black pixels (first frame sample): ${firstFrame.sampleNonBlackPixels}/${firstFrame.sampleTotal}`);
}
console.log(`Status: ${frameCount > 0 ? (firstFrame.sampleNonBlackPixels > 0 ? "PASS — receiving video" : "WARN — frames are all black") : "FAIL — no frames received"}`);

receiver.destroy();
finder.destroy();
process.exit(0);
