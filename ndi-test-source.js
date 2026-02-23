// Sends a color-cycling NDI test pattern so you can test NDI input detection
import grandi from "grandi";

const WIDTH = 640;
const HEIGHT = 480;
const FPS = 30;

const sender = await grandi.send({ name: "ShaderClaw Test Pattern" });
console.log(`NDI test source broadcasting as "ShaderClaw Test Pattern" (${WIDTH}x${HEIGHT} @ ${FPS}fps)`);
console.log("Press Ctrl+C to stop.\n");

let frame = 0;
const pixels = Buffer.alloc(WIDTH * HEIGHT * 4);

setInterval(() => {
  const t = frame / FPS;

  // Color bars that cycle over time
  for (let y = 0; y < HEIGHT; y++) {
    for (let x = 0; x < WIDTH; x++) {
      const i = (y * WIDTH + x) * 4;
      const bar = Math.floor((x / WIDTH) * 8);
      const pulse = Math.sin(t * 2 + bar * 0.5) * 0.3 + 0.7;

      // SMPTE-ish color bars with animation
      const colors = [
        [192, 192, 192], [192, 192, 0], [0, 192, 192], [0, 192, 0],
        [192, 0, 192], [192, 0, 0], [0, 0, 192], [16, 16, 16],
      ];
      const c = colors[bar] || [0, 0, 0];
      pixels[i] = Math.round(c[0] * pulse);     // R
      pixels[i + 1] = Math.round(c[1] * pulse); // G
      pixels[i + 2] = Math.round(c[2] * pulse); // B
      pixels[i + 3] = 255;                       // A
    }
  }

  sender.video({
    data: pixels,
    fourCC: grandi.FOURCC_RGBA,
    xres: WIDTH,
    yres: HEIGHT,
    frameRateN: FPS * 1000,
    frameRateD: 1001,
    lineStrideBytes: WIDTH * 4,
    pictureAspectRatio: WIDTH / HEIGHT,
    frameFormatType: grandi.FORMAT_TYPE_PROGRESSIVE,
  });

  frame++;
  if (frame % FPS === 0) {
    const tally = sender.tally(0);
    const viewers = tally?.onProgram ? " [ON PROGRAM]" : "";
    process.stdout.write(`\rFrame ${frame} (${Math.round(t)}s)${viewers}   `);
  }
}, 1000 / FPS);

process.on("SIGINT", () => {
  console.log("\nStopping...");
  sender.destroy();
  process.exit(0);
});
