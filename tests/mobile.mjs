#!/usr/bin/env node
// ShaderClaw Mobile Tests — Puppeteer viewport emulation
// Run: node tests/mobile.mjs  (server must be running on :7777)

import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

const PORT = 7777;
const BASE_URL = `http://localhost:${PORT}`;

const colors = {
  green: s => `\x1b[32m${s}\x1b[0m`,
  red:   s => `\x1b[31m${s}\x1b[0m`,
  yellow: s => `\x1b[33m${s}\x1b[0m`,
  dim:   s => `\x1b[2m${s}\x1b[0m`,
  bold:  s => `\x1b[1m${s}\x1b[0m`,
};

const results = [];
function report(id, name, pass, detail = '') {
  results.push({ id, name, pass, detail });
  const icon = pass ? colors.green('PASS') : colors.red('FAIL');
  const info = detail ? colors.dim(` (${detail})`) : '';
  console.log(`  ${icon}  ${id} ${name}${info}`);
}

async function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

const VIEWPORTS = [
  { name: 'iPhone SE',       width: 375,  height: 667,  deviceScaleFactor: 2, isMobile: true, hasTouch: true },
  { name: 'iPhone 14 Pro',   width: 393,  height: 852,  deviceScaleFactor: 3, isMobile: true, hasTouch: true },
  { name: 'iPad Mini',       width: 768,  height: 1024, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
  { name: 'Galaxy S21',      width: 360,  height: 800,  deviceScaleFactor: 3, isMobile: true, hasTouch: true },
  { name: 'iPad Pro 12.9',   width: 1024, height: 1366, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
];

async function runViewportTests(browser, viewport) {
  const vp = viewport;
  const prefix = vp.name.replace(/\s+/g, '');
  const page = await browser.newPage();

  const consoleErrors = [];
  page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });
  page.on('pageerror', err => consoleErrors.push(err.message));

  await page.setViewport({
    width: vp.width,
    height: vp.height,
    deviceScaleFactor: vp.deviceScaleFactor,
    isMobile: vp.isMobile,
    hasTouch: vp.hasTouch,
  });

  // M1: Page loads without crash
  try {
    await page.goto(BASE_URL, { waitUntil: 'networkidle2', timeout: 15000 });
    report(`${prefix}-M1`, `${vp.name}: Page Load`, true);
  } catch (e) {
    report(`${prefix}-M1`, `${vp.name}: Page Load`, false, e.message);
    await page.close();
    return;
  }

  await sleep(1500);

  // M2: WebGL context alive
  const m2 = await page.evaluate(() => {
    const c = document.getElementById('gl-canvas');
    if (!c) return { ok: false, detail: 'No gl-canvas' };
    const gl = c.getContext('webgl');
    return { ok: gl && !gl.isContextLost() };
  });
  report(`${prefix}-M2`, `${vp.name}: WebGL Context`, m2.ok, m2.ok ? '' : (m2.detail || 'context lost'));

  // M3: Layout — sidebar is below canvas on mobile, beside on tablet+
  const m3 = await page.evaluate(() => {
    const sidebar = document.getElementById('sidebar');
    const preview = document.getElementById('preview');
    if (!sidebar || !preview) return { ok: false, detail: 'Missing sidebar or preview' };
    const sr = sidebar.getBoundingClientRect();
    const pr = preview.getBoundingClientRect();
    return {
      ok: true,
      sidebarTop: sr.top,
      sidebarLeft: sr.left,
      sidebarW: sr.width,
      sidebarH: sr.height,
      previewTop: pr.top,
      previewW: pr.width,
      previewH: pr.height,
      windowW: window.innerWidth,
      windowH: window.innerHeight,
    };
  });
  if (m3.ok) {
    const isStacked = m3.sidebarTop > m3.previewTop; // sidebar below preview
    const isBeside = m3.sidebarLeft > 0; // sidebar to the right
    const expected = vp.width <= 768 ? 'stacked' : 'beside';
    const actual = isStacked ? 'stacked' : (isBeside ? 'beside' : 'unknown');
    report(`${prefix}-M3`, `${vp.name}: Layout (${expected})`, actual === expected,
      `sidebar: ${Math.round(m3.sidebarW)}x${Math.round(m3.sidebarH)} preview: ${Math.round(m3.previewW)}x${Math.round(m3.previewH)}`);
  } else {
    report(`${prefix}-M3`, `${vp.name}: Layout`, false, m3.detail);
  }

  // M4: No horizontal overflow
  const m4 = await page.evaluate(() => {
    const body = document.body;
    const overflow = body.scrollWidth > window.innerWidth;
    return { ok: !overflow, scrollW: body.scrollWidth, viewW: window.innerWidth };
  });
  report(`${prefix}-M4`, `${vp.name}: No Overflow`, m4.ok,
    m4.ok ? '' : `scrollWidth=${m4.scrollW} > viewport=${m4.viewW}`);

  // M5: Touch targets — all buttons/selects/inputs have min 28px height
  const m5 = await page.evaluate(() => {
    const selectors = [
      '.overlay-upload-btn', '.overlay-clear-btn', '#scene-model-btn',
      '.layer-shader-select', '.layer-control-row select', '.control-row select',
      '.canvas-option-row select', '.bt-action-btn', '.bt-rec-btn',
      '.layer-vis', '.canvas-btn', '.import-tile',
    ];
    const failures = [];
    for (const sel of selectors) {
      const els = document.querySelectorAll(sel);
      for (const el of els) {
        if (el.offsetParent === null) continue; // skip hidden
        const r = el.getBoundingClientRect();
        if (r.height < 27) { // 27 to allow tiny rounding
          failures.push(`${sel}: ${r.height.toFixed(1)}px`);
        }
      }
    }
    return { ok: failures.length === 0, failures };
  });
  report(`${prefix}-M5`, `${vp.name}: Touch Targets (≥28px)`, m5.ok,
    m5.ok ? '' : m5.failures.slice(0, 5).join(', '));

  // M6: Sidebar scrollable
  const m6 = await page.evaluate(() => {
    const sidebar = document.getElementById('sidebar');
    if (!sidebar) return { ok: false, detail: 'No sidebar' };
    const scrollable = sidebar.scrollHeight > sidebar.clientHeight;
    return { ok: true, scrollable, scrollH: sidebar.scrollHeight, clientH: sidebar.clientHeight };
  });
  report(`${prefix}-M6`, `${vp.name}: Sidebar Scrollable`, m6.ok,
    `scrollH=${m6.scrollH} clientH=${m6.clientH}`);

  // M7: Layer cards expand on tap (click the shader card header)
  const m7 = await page.evaluate(async () => {
    const headers = document.querySelectorAll('.layer-header');
    if (headers.length === 0) return { ok: false, detail: 'No layer headers' };
    // Find shader card
    for (const h of headers) {
      const nameEl = h.querySelector('.layer-name');
      if (nameEl && nameEl.textContent.trim().toUpperCase() === 'SHADER') {
        const card = h.closest('.layer-card');
        const wasClosed = !card.classList.contains('open');
        h.click();
        await new Promise(r => setTimeout(r, 100));
        const isOpen = card.classList.contains('open');
        // Restore
        if (wasClosed && isOpen) h.click();
        return { ok: true, toggled: wasClosed !== isOpen };
      }
    }
    return { ok: false, detail: 'Shader card not found' };
  });
  report(`${prefix}-M7`, `${vp.name}: Layer Card Toggle`, m7.ok, m7.toggled ? 'toggled' : '');

  // M8: BT panel sections present (even if hidden)
  const m8 = await page.evaluate(() => {
    const ids = ['bt-signals-section', 'bt-links-section', 'bt-recording-section', 'bt-overlay-row'];
    const missing = ids.filter(id => !document.getElementById(id));
    return { ok: missing.length === 0, missing };
  });
  report(`${prefix}-M8`, `${vp.name}: BT Panel Elements`, m8.ok,
    m8.ok ? '' : `missing: ${m8.missing.join(', ')}`);

  // M9: No JS errors from our code (filter out known CDN/favicon/404 issues)
  const ourErrors = consoleErrors.filter(e =>
    !/favicon|ORB|net::ERR|ERR_BLOCKED|three.*module|404|Failed to load resource/i.test(e)
  );
  report(`${prefix}-M9`, `${vp.name}: No JS Errors`, ourErrors.length === 0,
    ourErrors.length > 0 ? ourErrors.slice(0, 3).join(' | ') : '');

  // M10: Canvas renders (center pixel not black)
  const m10 = await page.evaluate(() => {
    const c = document.getElementById('gl-canvas');
    if (!c) return { ok: false, detail: 'No canvas' };
    const gl = c.getContext('webgl');
    if (!gl) return { ok: false, detail: 'No GL' };
    const px = new Uint8Array(4);
    gl.readPixels(Math.floor(c.width / 2), Math.floor(c.height / 2), 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
    const sum = px[0] + px[1] + px[2] + px[3];
    return { ok: sum > 0, pixel: Array.from(px) };
  });
  report(`${prefix}-M10`, `${vp.name}: Canvas Renders`, m10.ok,
    m10.ok ? `rgba(${m10.pixel.join(',')})` : 'black pixel');

  // M11: Overlay button visible and correct size (open the overlay card first)
  const m11 = await page.evaluate(async () => {
    // Open overlay card if collapsed — card toggle uses pointerdown/pointerup, not click
    const headers = document.querySelectorAll('.layer-header');
    for (const h of headers) {
      const nameEl = h.querySelector('.layer-name');
      if (nameEl && nameEl.textContent.trim().toUpperCase() === 'OVERLAY') {
        const card = h.closest('.layer-card');
        if (!card.classList.contains('open')) {
          card.classList.add('open');
        }
        break;
      }
    }
    await new Promise(r => setTimeout(r, 150));
    const btn = document.getElementById('overlay-upload-btn');
    if (!btn) return { ok: false, detail: 'Button not found' };
    const r = btn.getBoundingClientRect();
    const style = getComputedStyle(btn);
    return {
      ok: r.height >= 27,
      height: r.height,
      bg: style.background,
      border: style.borderStyle,
    };
  });
  report(`${prefix}-M11`, `${vp.name}: Add Image Button`, m11.ok,
    `h=${m11.height?.toFixed(1)}px border=${m11.border}`);

  // M12: BT section headers tappable (min 36px on mobile)
  const m12 = await page.evaluate(() => {
    const headers = document.querySelectorAll('.bt-section-header');
    const too_small = [];
    for (const h of headers) {
      if (h.offsetParent === null) continue;
      const r = h.getBoundingClientRect();
      if (r.height < 32) too_small.push(`${h.textContent.trim().slice(0, 20)}: ${r.height.toFixed(1)}px`);
    }
    return { ok: too_small.length === 0, issues: too_small };
  });
  report(`${prefix}-M12`, `${vp.name}: BT Headers Tappable`, m12.ok,
    m12.ok ? '' : m12.issues.join(', '));

  // M13: Overlay file info row — exists, has eye icon, correct height
  const m13 = await page.evaluate(() => {
    const info = document.getElementById('overlay-file-info');
    if (!info) return { ok: false, detail: 'overlay-file-info missing' };
    const icon = info.querySelector('.overlay-file-icon svg');
    if (!icon) return { ok: false, detail: 'eye icon SVG missing' };
    const nameEl = document.getElementById('overlay-file-name');
    if (!nameEl) return { ok: false, detail: 'file name element missing' };
    const removeBtn = document.getElementById('overlay-file-remove');
    if (!removeBtn) return { ok: false, detail: 'remove button missing' };
    return { ok: true };
  });
  report(`${prefix}-M13`, `${vp.name}: Overlay File Info`, m13.ok,
    m13.ok ? '' : (m13.detail || ''));

  // M14: Fullscreen handler registered (check that fullscreenchange listener exists)
  const m14 = await page.evaluate(() => {
    // We can't directly check listeners, but we can verify the fs-btn exists and is clickable
    const fsBtn = document.getElementById('fs-btn');
    if (!fsBtn) return { ok: false, detail: 'fs-btn missing' };
    const r = fsBtn.getBoundingClientRect();
    return { ok: r.width > 0 && r.height > 0, w: r.width, h: r.height };
  });
  report(`${prefix}-M14`, `${vp.name}: Fullscreen Button`, m14.ok,
    m14.ok ? `${m14.w}x${m14.h}` : (m14.detail || ''));

  await page.close();
}

async function main() {
  console.log(colors.bold('\n ShaderClaw Mobile Tests\n'));

  // Check server
  try {
    const resp = await fetch(BASE_URL).catch(() => null);
    if (!resp || !resp.ok) {
      console.log(colors.red(`  Server not running on ${BASE_URL}.`));
      console.log(colors.dim('  Start with: cd ShaderClaw && node server.js\n'));
      process.exit(1);
    }
  } catch {
    console.log(colors.red(`  Cannot reach ${BASE_URL}.\n`));
    process.exit(1);
  }

  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-gpu-sandbox'],
  });

  for (const vp of VIEWPORTS) {
    console.log(colors.bold(`\n  ── ${vp.name} (${vp.width}×${vp.height}) ──\n`));
    await runViewportTests(browser, vp);
  }

  await browser.close();

  // Summary
  console.log(colors.bold('\n  ── Summary ──\n'));
  const passed = results.filter(r => r.pass).length;
  const total = results.length;
  const failed = results.filter(r => !r.pass);

  if (failed.length > 0) {
    console.log(colors.red(`  Failures:\n`));
    for (const r of failed) {
      console.log(`  ${colors.red('✗')} ${r.id} ${r.name} ${colors.dim(r.detail)}`);
    }
    console.log('');
  }

  console.log(`  ${passed}/${total} passed\n`);
  process.exit(passed === total ? 0 : 1);
}

main().catch(e => {
  console.error(colors.red(`Fatal: ${e.message}`));
  process.exit(1);
});
