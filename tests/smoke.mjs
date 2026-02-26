#!/usr/bin/env node
// ShaderClaw Smoke Tests — Puppeteer against localhost:7777
// Run: node tests/smoke.mjs  (server must be running)

import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');

import { readFile, readdir } from 'fs/promises';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '..');
const SHADERS_DIR = join(ROOT, 'shaders');
const PORT = 7777;
const BASE_URL = `http://localhost:${PORT}`;

// ─── Helpers ───────────────────────────────────────────────
const colors = {
  green: s => `\x1b[32m${s}\x1b[0m`,
  red: s => `\x1b[31m${s}\x1b[0m`,
  yellow: s => `\x1b[33m${s}\x1b[0m`,
  dim: s => `\x1b[2m${s}\x1b[0m`,
  bold: s => `\x1b[1m${s}\x1b[0m`,
};

const results = [];
function report(id, name, pass, detail = '') {
  results.push({ id, name, pass, detail });
  const icon = pass ? colors.green('PASS') : colors.red('FAIL');
  const info = detail ? colors.dim(` (${detail})`) : '';
  console.log(`  ${icon}  ${id} ${name}${info}`);
}

async function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

const VERT_SHADER = `attribute vec2 position;
varying vec2 isf_FragNormCoord;
void main() {
    isf_FragNormCoord = position * 0.5 + 0.5;
    gl_Position = vec4(position, 0.0, 1.0);
}`;

// Replicate the ISF parsing + header building from index.html buildFragmentShader()
function buildFragmentShader(source) {
  // Parse ISF JSON block
  const match = source.match(/\/\*\s*(\{[\s\S]*?\})\s*\*\//);
  let meta = null, glsl = source.trim(), inputs = [];
  if (match) {
    try {
      meta = JSON.parse(match[1]);
      glsl = source.slice(source.indexOf(match[0]) + match[0].length).trim();
      inputs = meta.INPUTS || [];
    } catch {}
  }

  // Convert ISF inputs to uniform declarations
  function isfInputToUniform(input) {
    const t = input.TYPE;
    if (t === 'float') return `uniform float ${input.NAME};`;
    if (t === 'color') return `uniform vec4 ${input.NAME};`;
    if (t === 'bool') return `uniform bool ${input.NAME};`;
    if (t === 'point2D') return `uniform vec2 ${input.NAME};`;
    if (t === 'image') return `uniform sampler2D ${input.NAME};`;
    if (t === 'long') return `uniform float ${input.NAME};`;
    if (t === 'text') {
      const maxLen = input.MAX_LENGTH || 12;
      const lines = [];
      for (let i = 0; i < maxLen; i++) lines.push(`uniform float ${input.NAME}_${i};`);
      lines.push(`uniform float ${input.NAME}_len;`);
      return lines.join('\n');
    }
    return `// unknown type: ${t} ${input.NAME}`;
  }

  const uniformLines = inputs.map(isfInputToUniform);

  // Auto-declare ISF PASSES targets as sampler2D uniforms (for multi-pass shaders)
  const passUniforms = [];
  if (meta && Array.isArray(meta.PASSES)) {
    for (const pass of meta.PASSES) {
      if (pass.TARGET) passUniforms.push(`uniform sampler2D ${pass.TARGET};`);
    }
  }

  const headerParts = [
    'precision highp float;',
    'uniform float TIME;',
    'uniform vec2 RENDERSIZE;',
    'uniform int PASSINDEX;',
    'uniform int FRAMEINDEX;',
    'varying vec2 isf_FragNormCoord;',
    '#define IMG_NORM_PIXEL(img, coord) texture2D(img, coord)',
    '#define IMG_PIXEL(img, coord) texture2D(img, coord / RENDERSIZE)',
    'uniform sampler2D audioFFT;',
    'uniform float audioLevel;',
    'uniform float audioBass;',
    'uniform float audioMid;',
    'uniform float audioHigh;',
    'uniform sampler2D varFontTex;',
    'uniform sampler2D fontAtlasTex;',
    'uniform float useFontAtlas;',
    'uniform float _voiceGlitch;',
    'uniform sampler2D _maskTex;',
    'uniform float _maskMode;',
    'uniform float _maskFlip;',
    'uniform float _maskFlipV;',
    'uniform float _transparentBg;',
    'uniform vec2 mousePos;',
    'uniform vec2 mouseDelta;',
    ...passUniforms,
    ...uniformLines,
    ''
  ];
  const header = headerParts.join('\n');

  const cleaned = glsl.replace(/#version\s+\d+.*/g, '');

  // Wrap main() to inject mask post-processing (mirroring index.html)
  let body = header + cleaned;
  const mainRe = /void\s+main\s*\(\s*\)/;
  if (mainRe.test(body)) {
    body = body.replace(mainRe, 'void _shaderMain()');
    body += `
void main() {
    _shaderMain();
    if (_maskMode > 0.5) {
        vec2 muv = gl_FragCoord.xy / RENDERSIZE.xy;
        if (_maskFlip > 0.5) muv.x = 1.0 - muv.x;
        if (_maskFlipV > 0.5) muv.y = 1.0 - muv.y;
        vec4 _m = texture2D(_maskTex, muv);
        float _lum = dot(_m.rgb, vec3(0.299, 0.587, 0.114));
        if (_maskMode > 1.5) _lum = 1.0 - _lum;
        gl_FragColor.rgb *= _lum;
    }
}
`;
  }

  return body;
}

// ─── T7: Offline Shader Compile ────────────────────────────
async function runOfflineCompileTests() {
  console.log(colors.bold('\n  Offline shader compilation\n'));

  const manifest = JSON.parse(await readFile(join(SHADERS_DIR, 'manifest.json'), 'utf-8'));
  const shaderEntries = manifest.filter(e => e.file.endsWith('.fs'));
  const failures = [];

  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'], protocolTimeout: 30000 });

  for (const entry of shaderEntries) {
    const fsPath = join(SHADERS_DIR, entry.file);
    let fragSource;
    try {
      fragSource = await readFile(fsPath, 'utf-8');
    } catch {
      failures.push(`${entry.file}: file not found`);
      continue;
    }

    // Build fragment shader using the same pipeline as the app
    const fullFrag = buildFragmentShader(fragSource);

    let page;
    try {
      page = await browser.newPage();
      await page.setContent('<canvas id="c" width="1" height="1"></canvas>');

      const result = await Promise.race([
        page.evaluate(({ vertSource, fragSource }) => {
          const canvas = document.getElementById('c');
          const gl = canvas.getContext('webgl');
          if (!gl) return { ok: false, error: 'No WebGL context' };

          function compileShader(type, src) {
            const s = gl.createShader(type);
            if (!s) return { ok: false, error: 'createShader returned null' };
            gl.shaderSource(s, src);
            gl.compileShader(s);
            if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) {
              const log = gl.getShaderInfoLog(s);
              gl.deleteShader(s);
              return { ok: false, error: log };
            }
            return { ok: true, shader: s };
          }

          const vs = compileShader(gl.VERTEX_SHADER, vertSource);
          if (!vs.ok) return { ok: false, error: `Vertex: ${vs.error}` };

          const fs = compileShader(gl.FRAGMENT_SHADER, fragSource);
          if (!fs.ok) {
            gl.deleteShader(vs.shader);
            return { ok: false, error: `Fragment: ${fs.error}` };
          }

          const prog = gl.createProgram();
          gl.attachShader(prog, vs.shader);
          gl.attachShader(prog, fs.shader);
          gl.bindAttribLocation(prog, 0, 'position');
          gl.linkProgram(prog);

          gl.deleteShader(vs.shader);
          gl.deleteShader(fs.shader);

          if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
            const log = gl.getProgramInfoLog(prog);
            gl.deleteProgram(prog);
            return { ok: false, error: `Link: ${log}` };
          }

          gl.deleteProgram(prog);
          return { ok: true };
        }, { vertSource: VERT_SHADER, fragSource: fullFrag }),
        sleep(20000).then(() => ({ ok: false, error: 'Compilation timeout (20s)' }))
      ]);

      if (!result.ok) {
        failures.push(`${entry.title} (${entry.file}): ${result.error}`);
      }
    } catch (e) {
      failures.push(`${entry.title} (${entry.file}): ${e.message}`);
    } finally {
      if (page) await page.close().catch(() => {});
    }
  }

  await browser.close();

  if (failures.length > 0) {
    report('T7', 'Offline Shader Compile', false, `${failures.length}/${shaderEntries.length} failed`);
    for (const f of failures) console.log(`        ${colors.red('x')} ${f}`);
  } else {
    report('T7', 'Offline Shader Compile', true, `${shaderEntries.length} shaders`);
  }
}

// ─── T8–T9: NDI tests via direct WebSocket ─────────────────
async function runNdiTests() {
  console.log(colors.bold('\n  NDI tests (via WebSocket)\n'));

  const WebSocket = (await import('ws')).default;

  function wsSendAndWait(ws, msg, timeoutMs = 5000) {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('WS timeout')), timeoutMs);
      const handler = (data) => {
        try {
          const resp = JSON.parse(data.toString());
          if (resp.id === msg.id) {
            ws.removeListener('message', handler);
            clearTimeout(timer);
            if (resp.error) reject(new Error(resp.error));
            else resolve(resp.result);
          }
        } catch {}
      };
      ws.on('message', handler);
      ws.send(JSON.stringify(msg));
    });
  }

  let ws;
  try {
    ws = await new Promise((resolve, reject) => {
      const sock = new WebSocket(`ws://localhost:${PORT}`);
      sock.on('open', () => resolve(sock));
      sock.on('error', reject);
      setTimeout(() => reject(new Error('WS connect timeout')), 3000);
    });
  } catch (e) {
    report('T8', 'NDI Send Start', false, `WS connect failed: ${e.message}`);
    report('T9', 'NDI Source Discovery', false, 'skipped (no WS)');
    return;
  }

  // T8: NDI Send Start
  try {
    const startResp = await wsSendAndWait(ws, { id: -100, action: 'ndi_send_start', params: { name: 'ShaderClaw-Test', width: 960, height: 540 } });
    const stopResp = await wsSendAndWait(ws, { id: -101, action: 'ndi_send_stop', params: {} });
    report('T8', 'NDI Send Start', startResp && startResp.ok === true, 'start+stop ok');
  } catch (e) {
    report('T8', 'NDI Send Start', false, e.message);
  }

  // T9: NDI Source Discovery
  try {
    const resp = await wsSendAndWait(ws, { id: -102, action: 'ndi_find_sources', params: {} });
    const hasSources = resp && Array.isArray(resp.sources);
    report('T9', 'NDI Source Discovery', hasSources, hasSources ? `${resp.sources.length} sources` : JSON.stringify(resp));
  } catch (e) {
    report('T9', 'NDI Source Discovery', false, e.message);
  }

  ws.close();
}

// ─── T1–T6: Browser tests against running server ──────────
async function runBrowserTests() {
  console.log(colors.bold('\n  Browser tests (server must be running on :7777)\n'));

  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-gpu-sandbox'],
  });

  const page = await browser.newPage();
  const consoleErrors = [];
  page.on('console', msg => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  });
  page.on('pageerror', err => consoleErrors.push(err.message));

  // ── T6: Performance Gate — page load + first render under 15s ──
  const loadStart = Date.now();
  try {
    await page.goto(BASE_URL, { waitUntil: 'networkidle2', timeout: 15000 });
  } catch (e) {
    report('T6', 'Performance Gate', false, e.message);
    await browser.close();
    return;
  }

  // Wait for renderer to be available
  await page.waitForFunction(() => {
    return window.shaderClaw && typeof window.shaderClaw.getErrors === 'function';
  }, { timeout: 5000 }).catch(() => {});

  // Give it a moment to render a first frame
  await sleep(1000);

  const loadTime = Date.now() - loadStart;
  report('T6', 'Performance Gate', loadTime < 15000, `${loadTime}ms`);

  // ── T1: WebGL Health ──
  const t1 = await page.evaluate(() => {
    const canvas = document.querySelector('canvas');
    if (!canvas) return { ok: false, reason: 'No canvas element' };
    const gl = canvas.getContext('webgl');
    if (!gl) return { ok: false, reason: 'No GL context on canvas' };
    if (gl.isContextLost()) return { ok: false, reason: 'Context is lost' };
    return { ok: true };
  });
  report('T1', 'WebGL Health', t1.ok, t1.ok ? '' : t1.reason);

  // ── T2: Shader Compilation (API + default shader check) ──
  // Full per-shader compilation is tested by T7 (offline). T2 verifies the runtime
  // compilation API works with the default shader and a simple manifest shader.
  const t2 = await page.evaluate(() => {
    try {
      const api = window.shaderClaw;
      if (!api) return { ok: false, reason: 'No shaderClaw API' };
      if (!api.loadSource) return { ok: false, reason: 'No loadSource method' };
      if (!api.getErrors) return { ok: false, reason: 'No getErrors method' };
      // Default shader should already be compiled on page load
      const errors = api.getErrors();
      if (errors) return { ok: false, reason: `Default shader error: ${errors}` };
      return { ok: true };
    } catch (e) {
      return { ok: false, reason: e.message };
    }
  });
  // Also compile the gradient shader (simplest manifest entry)
  const gradientForT2 = await readFile(join(SHADERS_DIR, 'no_mans_sky_gradients.fs'), 'utf-8');
  const t2b = await Promise.race([
    page.evaluate(async (code) => {
      try {
        window.shaderClaw.loadSource(code);
        await new Promise(r => setTimeout(r, 500));
        const errors = window.shaderClaw.getErrors();
        return { ok: !errors, errors };
      } catch (e) {
        return { ok: false, errors: e.message };
      }
    }, gradientForT2),
    sleep(10000).then(() => ({ ok: false, errors: 'Compilation timeout (10s)' })),
  ]);
  const t2pass = t2.ok && t2b.ok;
  const t2detail = !t2.ok ? t2.reason : !t2b.ok ? `Gradient: ${t2b.errors}` : 'API + gradient OK';
  report('T2', 'Shader Compilation', t2pass, t2detail);

  // ── T3: Rendering — gradient was loaded in T2, check center pixel ──
  // Give an extra frame to render
  await sleep(500);

  const t3 = await page.evaluate(() => {
    const canvas = document.querySelector('canvas');
    if (!canvas) return { ok: false, reason: 'No canvas' };
    const gl = canvas.getContext('webgl');
    if (!gl) return { ok: false, reason: 'No GL context' };

    const w = canvas.width, h = canvas.height;
    const cx = Math.floor(w / 2), cy = Math.floor(h / 2);
    const pixel = new Uint8Array(4);
    gl.readPixels(cx, cy, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
    const sum = pixel[0] + pixel[1] + pixel[2] + pixel[3];
    return {
      ok: sum > 0,
      reason: sum > 0 ? '' : `Center pixel is all zeros: [${pixel.join(',')}]`,
      pixel: [pixel[0], pixel[1], pixel[2], pixel[3]],
    };
  });
  report('T3', 'Rendering', t3.ok, t3.ok ? `rgba(${t3.pixel.join(',')})` : t3.reason);

  // ── T4: Error Bar Clean ──
  const t4 = await page.evaluate(() => {
    const bar = document.getElementById('error-bar');
    if (!bar) return { ok: true, reason: 'No error-bar element (ok)' };
    return { ok: !bar.classList.contains('show'), reason: bar.textContent || '' };
  });
  report('T4', 'Error Bar Clean', t4.ok, t4.ok ? '' : `Error showing: ${t4.reason}`);

  // ── T5: Context Stability — gl.isContextLost() still false after 5s ──
  console.log(colors.dim('        (waiting 5s for context stability...)'));
  await sleep(5000);
  const t5 = await page.evaluate(() => {
    const canvas = document.querySelector('canvas');
    if (!canvas) return { ok: false, reason: 'No canvas' };
    const gl = canvas.getContext('webgl');
    if (!gl) return { ok: false, reason: 'No GL context' };
    return { ok: !gl.isContextLost(), reason: gl.isContextLost() ? 'Context lost' : '' };
  });
  report('T5', 'Context Stability', t5.ok, t5.ok ? '5s stable' : t5.reason);

  // ── T8 & T9: NDI tests via direct WebSocket ──
  await runNdiTests();

  // ── T10: Dynamic Tools — load shader, verify param tools registered ──
  // We test this by loading the gradient shader which has ISF inputs,
  // and verifying that the server created param_ tools
  const t10 = await page.evaluate(async () => {
    try {
      // Load gradient shader via loadSource — this triggers registerDynamicTools on server
      const gradCode = await fetch('/shaders/no_mans_sky_gradients.fs').then(r => r.text());
      window.shaderClaw.loadSource(gradCode);
      await new Promise(r => setTimeout(r, 300));
      const inputs = window.shaderClaw.getInputs();
      return { ok: inputs && inputs.length > 0, count: inputs ? inputs.length : 0 };
    } catch (e) {
      return { ok: false, detail: e.message };
    }
  });
  report('T10', 'Dynamic Tools', t10.ok, t10.ok ? `${t10.count} params` : (t10.detail || 'no inputs'));

  // ── T11: Preset Round-trip — save → list → delete (filesystem test) ──
  const t11 = await (async () => {
    try {
      const { writeFile: wf, readFile: rf, unlink: ul, mkdir: mk, readdir: rd } = await import('fs/promises');
      const presetsDir = join(ROOT, 'presets');
      await mk(presetsDir, { recursive: true });

      // Save
      const testPreset = { name: '_smoke_test', description: 'test', timestamp: new Date().toISOString(), parameters: { speed: 1.0, color1: [1, 0, 0, 1] } };
      await wf(join(presetsDir, '_smoke_test.json'), JSON.stringify(testPreset, null, 2));

      // List — verify it appears
      const files = await rd(presetsDir);
      const listOk = files.includes('_smoke_test.json');

      // Load — verify round-trip
      const data = await rf(join(presetsDir, '_smoke_test.json'), 'utf-8');
      const loaded = JSON.parse(data);
      const loadOk = loaded.name === '_smoke_test' && loaded.parameters.speed === 1.0;

      // Delete
      await ul(join(presetsDir, '_smoke_test.json'));
      const filesAfter = await rd(presetsDir);
      const deleteOk = !filesAfter.includes('_smoke_test.json');

      return { ok: listOk && loadOk && deleteOk, detail: `list=${listOk} load=${loadOk} delete=${deleteOk}` };
    } catch (e) {
      return { ok: false, detail: e.message };
    }
  })();
  report('T11', 'Preset Round-trip', t11.ok, t11.ok ? 'save+list+load+delete ok' : t11.detail);

  // ── T12: Layer Visibility — toggle layers, no errors ──
  const t12 = await page.evaluate(async () => {
    try {
      if (!window.shaderClaw.setLayerVisibility) return { ok: false, detail: 'setLayerVisibility not defined' };
      // Toggle each layer off then back on
      for (const id of ['scene', 'shader', 'text']) {
        let r = window.shaderClaw.setLayerVisibility(id, false);
        if (!r.ok) return { ok: false, detail: `hide ${id}: ${r.error}` };
        r = window.shaderClaw.setLayerVisibility(id, true);
        if (!r.ok) return { ok: false, detail: `show ${id}: ${r.error}` };
      }
      await new Promise(r => setTimeout(r, 200));
      const layers = window.shaderClaw.getLayers();
      const allVisible = layers.every(l => l.visible);
      return { ok: allVisible, detail: allVisible ? '' : 'Not all visible after toggle' };
    } catch (e) {
      return { ok: false, detail: e.message };
    }
  });
  report('T12', 'Layer Visibility', t12.ok, t12.ok ? '' : t12.detail);

  // ── T13: Layer Opacity — set 0.5, no errors ──
  const t13 = await page.evaluate(async () => {
    try {
      if (!window.shaderClaw.setLayerOpacity) return { ok: false, detail: 'setLayerOpacity not defined' };
      for (const id of ['scene', 'shader', 'text']) {
        const r = window.shaderClaw.setLayerOpacity(id, 0.5);
        if (!r.ok) return { ok: false, detail: `${id}: ${r.error}` };
      }
      const layers = window.shaderClaw.getLayers();
      const allHalf = layers.every(l => Math.abs(l.opacity - 0.5) < 0.01);
      // Restore
      for (const id of ['scene', 'shader', 'text']) {
        window.shaderClaw.setLayerOpacity(id, 1.0);
      }
      return { ok: allHalf, detail: allHalf ? '' : 'Opacity not set correctly' };
    } catch (e) {
      return { ok: false, detail: e.message };
    }
  });
  report('T13', 'Layer Opacity', t13.ok, t13.ok ? '' : t13.detail);

  // ── T14: Multi-Shader — different shaders on shader+text layers compile ──
  const t14 = await page.evaluate(async () => {
    try {
      if (!window.shaderClaw.compileToLayer) return { ok: false, detail: 'compileToLayer not defined' };
      // Load gradient to shader layer
      const gradCode = await fetch('/shaders/no_mans_sky_gradients.fs').then(r => r.text());
      const r1 = window.shaderClaw.compileToLayer('shader', gradCode);
      if (!r1.ok) return { ok: false, detail: `shader layer: ${r1.errors}` };
      // Load sky to text layer
      const skyCode = await fetch('/shaders/sky.fs').then(r => r.text());
      const r2 = window.shaderClaw.compileToLayer('text', skyCode);
      if (!r2.ok) return { ok: false, detail: `text layer: ${r2.errors}` };
      await new Promise(r => setTimeout(r, 200));
      return { ok: true };
    } catch (e) {
      return { ok: false, detail: e.message };
    }
  });
  report('T14', 'Multi-Shader', t14.ok, t14.ok ? '' : t14.detail);

  // ── T15: Context Loss Recovery — force loss via WEBGL_lose_context, verify restore ──
  const t15 = await page.evaluate(async () => {
    try {
      const canvas = document.getElementById('gl-canvas');
      if (!canvas) return { ok: false, detail: 'No gl-canvas' };
      const gl = canvas.getContext('webgl');
      if (!gl) return { ok: false, detail: 'No GL context' };
      const ext = gl.getExtension('WEBGL_lose_context');
      if (!ext) return { ok: false, detail: 'WEBGL_lose_context not available' };
      // Force context loss
      ext.loseContext();
      await new Promise(r => setTimeout(r, 500));
      if (!gl.isContextLost()) return { ok: false, detail: 'Context not lost after loseContext()' };
      // Restore
      ext.restoreContext();
      await new Promise(r => setTimeout(r, 2000));
      const restored = !gl.isContextLost();
      return { ok: restored, detail: restored ? '' : 'Context still lost after restore' };
    } catch (e) {
      return { ok: false, detail: e.message };
    }
  });
  report('T15', 'Context Loss Recovery', t15.ok, t15.ok ? '' : t15.detail);

  // Check for WebGL errors in console
  const webglErrors = consoleErrors.filter(e =>
    /webgl|shader|vertex.*null|context.*lost/i.test(e)
  );
  if (webglErrors.length > 0) {
    console.log(colors.yellow(`\n  Console WebGL errors (${webglErrors.length}):`));
    for (const e of webglErrors.slice(0, 5)) console.log(`    ${colors.dim(e)}`);
  }

  await browser.close();
}

// ─── Main ──────────────────────────────────────────────────
async function main() {
  console.log(colors.bold('\n ShaderClaw Smoke Tests\n'));

  // T7 runs first (no server dependency for offline compile)
  await runOfflineCompileTests();

  // T1–T6 need the server running
  try {
    const resp = await fetch(BASE_URL).catch(() => null);
    if (!resp || !resp.ok) {
      console.log(colors.red(`\n  Server not running on ${BASE_URL}. Skipping browser tests T1-T6.`));
      console.log(colors.dim('  Start with: cd ShaderClaw && bun run server.js\n'));
    } else {
      await runBrowserTests();
    }
  } catch {
    console.log(colors.red(`\n  Cannot reach ${BASE_URL}. Skipping browser tests T1-T6.\n`));
  }

  // Summary
  console.log(colors.bold('\n  ── Summary ──\n'));
  const passed = results.filter(r => r.pass).length;
  const total = results.length;
  for (const r of results) {
    const icon = r.pass ? colors.green('✓') : colors.red('✗');
    console.log(`  ${icon} ${r.id} ${r.name}`);
  }
  console.log(`\n  ${passed}/${total} passed\n`);

  process.exit(passed === total ? 0 : 1);
}

main().catch(e => {
  console.error(colors.red(`Fatal: ${e.message}`));
  process.exit(1);
});
