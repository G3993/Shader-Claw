#!/usr/bin/env node
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'], protocolTimeout: 60000 });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080 });
await page.goto('http://localhost:7777', { waitUntil: 'networkidle2', timeout: 20000 });
await new Promise(r => setTimeout(r, 3000));

// Inject debug into the render pipeline
const debugInfo = await page.evaluate(() => {
  // Access internal vars through the shaderClaw closure
  // We need to check: compositorProgram, layer FBOs, sceneTexture, etc.

  // Get the GL context
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');

  const info = {};

  // Check how many GL programs exist
  info.contextLost = gl.isContextLost();
  info.activeMode = window.shaderClaw.getActiveMode();

  // Check layers via getLayers
  info.layers = window.shaderClaw.getLayers();

  // Check if shaderClaw has other useful methods
  info.apiMethods = Object.keys(window.shaderClaw);

  // Check the Three.js canvas
  const tc = document.getElementById('three-canvas');
  const tcCtx = tc.getContext('webgl2') || tc.getContext('webgl');
  if (tcCtx) {
    info.threeContextExists = true;
    info.threeContextLost = tcCtx.isContextLost();
    info.threeRenderer = tcCtx.getParameter(tcCtx.RENDERER);
  } else {
    info.threeContextExists = false;
  }

  // Read pixel from Three.js canvas
  if (tcCtx && !tcCtx.isContextLost()) {
    const px = new Uint8Array(4);
    tcCtx.readPixels(tc.width/2|0, tc.height/2|0, 1, 1, tcCtx.RGBA, tcCtx.UNSIGNED_BYTE, px);
    info.threePixel = Array.from(px);
  }

  // Check if the Three canvas has content via toDataURL
  info.threeDataUrlLen = tc.toDataURL().length;
  info.glDataUrlLen = c.toDataURL().length;

  return info;
});
console.log('Debug info:', JSON.stringify(debugInfo, null, 2));

// Now the key test: load gradient shader and check BOTH canvases
const gradientCode = require('fs').readFileSync('shaders/no_mans_sky_gradients.fs', 'utf-8');
await page.evaluate(code => window.shaderClaw.loadSource(code), gradientCode);
await new Promise(r => setTimeout(r, 2000));

const afterShader = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  const tc = document.getElementById('three-canvas');

  return {
    glDataUrlLen: c.toDataURL().length,
    threeDataUrlLen: tc.toDataURL().length,
    activeMode: window.shaderClaw.getActiveMode(),
    errors: window.shaderClaw.getErrors(),
    glDisplay: c.style.display,
    threeDisplay: tc.style.display,
  };
});
console.log('After shader:', JSON.stringify(afterShader, null, 2));

// THE KEY TEST: Does the compositionLoop actually render anything to the GL canvas?
// Stop the compositionLoop briefly and force a manual render
const manualRender = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');

  // Clear to red
  gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  gl.viewport(0, 0, c.width, c.height);
  gl.clearColor(1, 0, 0, 1);
  gl.clear(gl.COLOR_BUFFER_BIT);

  const px = new Uint8Array(4);
  gl.readPixels(c.width/2|0, c.height/2|0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);

  return {
    pixelAfterClear: Array.from(px),
    dataUrlLen: c.toDataURL().length,
  };
});
console.log('Manual red clear:', JSON.stringify(manualRender));

// Wait one frame for compositionLoop to overwrite
await new Promise(r => setTimeout(r, 50));

const afterFrame = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  const px = new Uint8Array(4);
  gl.readPixels(c.width/2|0, c.height/2|0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
  return {
    pixel: Array.from(px),
    dataUrlLen: c.toDataURL().length,
  };
});
console.log('After 1 frame:', JSON.stringify(afterFrame));

await browser.close();
