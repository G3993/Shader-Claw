const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');
(async () => {
  const browser = await puppeteer.launch({ headless: 'new', args: ['--no-sandbox','--disable-gpu'] });
  const page = await browser.newPage();
  const errors = [];
  const logs = [];
  page.on('console', msg => logs.push(msg.type() + ': ' + msg.text()));
  page.on('pageerror', err => errors.push(err.message));
  await page.goto('http://localhost:7777', { waitUntil: 'domcontentloaded', timeout: 15000 });
  await new Promise(r => setTimeout(r, 6000));
  const info = await page.evaluate(() => {
    return {
      title: document.title,
      canvases: document.querySelectorAll('canvas').length,
      debugText: (document.getElementById('debug-overlay') || {}).textContent || 'none',
      debugVisible: (document.getElementById('debug-overlay') || {}).style ? document.getElementById('debug-overlay').style.display : 'n/a',
      errorBar: (document.getElementById('error-bar') || {}).textContent || 'none',
      hasShaderClaw: typeof window.shaderClaw !== 'undefined',
      contextLost: typeof window._contextLost !== 'undefined' ? window._contextLost : 'undef',
    };
  });
  console.log('=== PAGE INFO ===');
  console.log(JSON.stringify(info, null, 2));
  console.log('=== PAGE ERRORS ===');
  errors.forEach(e => console.log('  ERR:', e.slice(0, 300)));
  console.log('=== CONSOLE (last 40) ===');
  logs.slice(-40).forEach(l => console.log('  ', l.slice(0, 300)));
  await browser.close();
})().catch(e => console.log('FATAL:', e.message));
