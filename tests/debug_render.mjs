#!/usr/bin/env node
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'], protocolTimeout: 60000 });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080 });
await page.goto('http://localhost:7777', { waitUntil: 'networkidle2', timeout: 20000 });
await new Promise(r => setTimeout(r, 3000));

// Check GL context
const glInfo = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  const attrs = gl.getContextAttributes();
  const r = window.isfRenderer;
  return {
    canvasW: c.width, canvasH: c.height,
    canvasDisplay: c.style.display,
    renderer: gl.getParameter(gl.RENDERER),
    preserveDrawingBuffer: attrs && attrs.preserveDrawingBuffer,
    hasIsfRenderer: !!r,
    hasProgram: !!(r && r.program),
    contextLost: gl.isContextLost(),
  };
});
console.log('GL Info:', JSON.stringify(glInfo, null, 2));

// Force render and read pixel
const pixel1 = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  const r = window.isfRenderer;
  if (r && r.program) r.render();
  const px = new Uint8Array(4);
  gl.readPixels(Math.floor(c.width / 2), Math.floor(c.height / 2), 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
  return Array.from(px);
});
console.log('Center pixel after render:', pixel1);

// Load simplest possible red shader
const redShader = `/*{
  "DESCRIPTION": "Red",
  "INPUTS": []
}*/
void main() { gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0); }`;

await page.evaluate(code => { window.shaderClaw.loadSource(code); }, redShader);
await new Promise(r => setTimeout(r, 1000));

const errors = await page.evaluate(() => window.shaderClaw.getErrors());
console.log('Red shader errors:', errors || 'none');

// Force render and read pixel
const pixel2 = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  const gl = c.getContext('webgl');
  const r = window.isfRenderer;
  if (r && r.program) r.render();
  const px = new Uint8Array(4);
  gl.readPixels(Math.floor(c.width / 2), Math.floor(c.height / 2), 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
  return Array.from(px);
});
console.log('Red shader pixel:', pixel2);

// Check the toDataURL
const dataUrl = await page.evaluate(() => {
  const c = document.getElementById('gl-canvas');
  return c.toDataURL('image/png').length;
});
console.log('toDataURL length:', dataUrl);

// Check if screenshot() MCP tool works
const screenshot = await page.evaluate(async () => {
  if (window.shaderClaw && window.shaderClaw.screenshot) {
    return (await window.shaderClaw.screenshot()).substring(0, 80);
  }
  return 'no screenshot method';
});
console.log('MCP screenshot:', screenshot);

await page.screenshot({ path: 'fix_red_test.png' });
await browser.close();
console.log('Done');
