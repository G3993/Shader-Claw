#!/usr/bin/env node
import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'], protocolTimeout: 60000 });
const page = await browser.newPage();
await page.setViewport({ width: 1920, height: 1080 });
await page.goto('http://localhost:7777', { waitUntil: 'networkidle2', timeout: 20000 });
await new Promise(r => setTimeout(r, 3000));

// Use MCP screenshot to check if rendering works
const screenshotLen = await page.evaluate(() => {
  return window.shaderClaw.screenshot().length;
});
console.log('Default shader screenshot length:', screenshotLen);

// Load red shader
const redShader = `/*{
  "DESCRIPTION": "Red",
  "INPUTS": []
}*/
void main() { gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0); }`;

await page.evaluate(code => window.shaderClaw.loadSource(code), redShader);
await new Promise(r => setTimeout(r, 500));

const redLen = await page.evaluate(() => {
  return window.shaderClaw.screenshot().length;
});
console.log('Red shader screenshot length:', redLen);
console.log('Red errors:', await page.evaluate(() => window.shaderClaw.getErrors()) || 'none');

// Check layers and shader state
const state = await page.evaluate(() => {
  const layers = window.shaderClaw.getLayers();
  const errs = window.shaderClaw.getErrors();
  // Check if the shader layer has a program by checking via compileToLayer result
  return { layers, errors: errs };
});
console.log('State:', JSON.stringify(state, null, 2));

// Save the MCP screenshot as a file to view it
const screenshotData = await page.evaluate(() => {
  return window.shaderClaw.screenshot();
});
const fs = require('fs');
const base64 = screenshotData.replace(/^data:image\/png;base64,/, '');
fs.writeFileSync('fix_mcp_screenshot.png', Buffer.from(base64, 'base64'));
console.log('MCP screenshot saved to fix_mcp_screenshot.png');

// Also save Puppeteer screenshot for comparison
await page.screenshot({ path: 'fix_puppeteer_screenshot.png' });
console.log('Puppeteer screenshot saved');

await browser.close();
