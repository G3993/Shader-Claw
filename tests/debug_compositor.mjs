#!/usr/bin/env node
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'], protocolTimeout: 60000 });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080 });

const logs = [];
page.on('console', msg => logs.push(msg.text()));
page.on('pageerror', err => logs.push('PAGE ERROR: ' + err.message));

await page.goto('http://localhost:7777', { waitUntil: 'networkidle2', timeout: 20000 });
await new Promise(r => setTimeout(r, 4000));

// Step 1: Debug render with scene only
console.log('\n=== STEP 1: Debug render (scene only) ===');
const render1 = await page.evaluate(() => window.shaderClaw._debugRender());
console.log(JSON.stringify(render1, null, 2));

// Step 2: Load gradient shader then debug render
console.log('\n=== STEP 2: Load gradient + debug render ===');
const gradientCode = require('fs').readFileSync('shaders/no_mans_sky_gradients.fs', 'utf-8');
await page.evaluate(code => window.shaderClaw.loadSource(code), gradientCode);
await new Promise(r => setTimeout(r, 2000));

const render2 = await page.evaluate(() => window.shaderClaw._debugRender());
console.log(JSON.stringify(render2, null, 2));

// Step 3: Test with the simplest possible compositor - just output red
console.log('\n=== STEP 3: Force solid red output ===');
const solidRed = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  gl.viewport(0, 0, c.width, c.height);
  gl.clearColor(1, 0, 0, 1);
  gl.clear(gl.COLOR_BUFFER_BIT);
  const px = new Uint8Array(4);
  gl.readPixels(c.width/2|0, c.height/2|0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
  return { pixel: Array.from(px) };
});
console.log('Solid red clear result:', JSON.stringify(solidRed));

// Step 4: Now test compositor shader with a minimal draw
console.log('\n=== STEP 4: Minimal compositor draw test ===');
const minDraw = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');

  // Create a minimal red shader
  const vs = gl.createShader(gl.VERTEX_SHADER);
  gl.shaderSource(vs, `attribute vec2 position;
void main() { gl_Position = vec4(position, 0.0, 1.0); }`);
  gl.compileShader(vs);

  const fs = gl.createShader(gl.FRAGMENT_SHADER);
  gl.shaderSource(fs, `precision highp float;
void main() { gl_FragColor = vec4(0.0, 1.0, 0.0, 1.0); }`);
  gl.compileShader(fs);

  const prog = gl.createProgram();
  gl.attachShader(prog, vs);
  gl.attachShader(prog, fs);
  gl.bindAttribLocation(prog, 0, 'position');
  gl.linkProgram(prog);

  const linked = gl.getProgramParameter(prog, gl.LINK_STATUS);

  gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  gl.viewport(0, 0, c.width, c.height);
  gl.useProgram(prog);

  // Use the same fullscreen triangle buffer
  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1, 3,-1, -1,3]), gl.STATIC_DRAW);
  gl.enableVertexAttribArray(0);
  gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
  gl.drawArrays(gl.TRIANGLES, 0, 3);

  const err = gl.getError();
  const px = new Uint8Array(4);
  gl.readPixels(c.width/2|0, c.height/2|0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);

  gl.deleteProgram(prog);
  gl.deleteShader(vs);
  gl.deleteShader(fs);
  gl.deleteBuffer(buf);

  return { linked, error: err === 0 ? 'none' : '0x'+err.toString(16), pixel: Array.from(px) };
});
console.log('Minimal draw result:', JSON.stringify(minDraw));

// Console logs
if (logs.length > 0) {
  console.log('\n=== Console logs (last 10) ===');
  for (const l of logs.slice(-10)) console.log('  ', l);
}

await browser.close();
