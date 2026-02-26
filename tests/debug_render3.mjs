#!/usr/bin/env node
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'], protocolTimeout: 60000 });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080 });

// Collect console output
const logs = [];
page.on('console', msg => logs.push(msg.text()));
page.on('pageerror', err => logs.push('PAGE ERROR: ' + err.message));

await page.goto('http://localhost:7777', { waitUntil: 'networkidle2', timeout: 20000 });
await new Promise(r => setTimeout(r, 3000));

// Check the compositor state
const compositorState = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');

  // Access ISF renderer and layers through closure inspection
  // Use shaderClaw API
  const layers = window.shaderClaw.getLayers();
  const mode = window.shaderClaw.getActiveMode();

  // Check if compositor program exists by looking at GL programs
  return {
    mode,
    layers,
    glCanvasDisplay: c.style.display,
    threeCanvasDisplay: document.getElementById('three-canvas').style.display,
    glCanvasSize: [c.width, c.height],
    contextLost: gl.isContextLost(),
  };
});
console.log('Compositor state:', JSON.stringify(compositorState, null, 2));

// Now load a shader and check what the compositor does
// Inject a debug flag into the compositionLoop
await page.evaluate(() => {
  // Patch renderCompositor to log when it runs
  window._compositorRanCount = 0;
  window._compositorDebug = {};

  const origRC = window.isfRenderer_renderCompositor;
  // We can't easily patch the method, but we can check layer FBOs
});

// Load gradient shader (simple)
const gradientCode = require('fs').readFileSync('shaders/no_mans_sky_gradients.fs', 'utf-8');
await page.evaluate(code => window.shaderClaw.loadSource(code), gradientCode);
await new Promise(r => setTimeout(r, 2000));

// Check layer state after loading shader
const afterLoad = await page.evaluate(() => {
  const layers = window.shaderClaw.getLayers();
  const errors = window.shaderClaw.getErrors();
  const mode = window.shaderClaw.getActiveMode();

  // Check GL canvas for actual pixel content
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');

  // Read multiple pixels
  const pixels = {};
  for (const [name, x, y] of [['center', c.width/2|0, c.height/2|0], ['topLeft', 10, c.height-10], ['bottomRight', c.width-10, 10]]) {
    const px = new Uint8Array(4);
    gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
    pixels[name] = Array.from(px);
  }

  // Check Three canvas
  const tc = document.getElementById('three-canvas');
  const ctx = tc.getContext('2d') || tc.getContext('webgl');

  return { mode, layers, errors, pixels,
    glDisplay: c.style.display, threeDisplay: tc.style.display,
    threeSize: [tc.width, tc.height] };
});
console.log('After shader load:', JSON.stringify(afterLoad, null, 2));

// Use the MCP screenshot and check what canvas it captures
const screenshotInfo = await page.evaluate(() => {
  const mode = window.shaderClaw.getActiveMode();
  const c = mode === 'scene' ? document.getElementById('three-canvas') : document.getElementById('gl-canvas');
  const url = c.toDataURL('image/png');
  return { mode, canvas: mode === 'scene' ? 'three-canvas' : 'gl-canvas', urlLen: url.length };
});
console.log('Screenshot info:', JSON.stringify(screenshotInfo));

// Force render to GL canvas and screenshot
const forceRender = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  // Force clear to red to test if GL canvas is actually visible
  gl.clearColor(1.0, 0.0, 0.0, 1.0);
  gl.clear(gl.COLOR_BUFFER_BIT);
  return c.toDataURL('image/png').length;
});
console.log('After red clear, toDataURL length:', forceRender);

await page.screenshot({ path: 'fix_debug_redclear.png' });

// Print console logs
if (logs.length > 0) {
  console.log('\nConsole output:');
  for (const l of logs.slice(-20)) console.log('  ', l);
}

await browser.close();
