const puppeteer = require('C:/Users/nofun/AppData/Roaming/npm/node_modules/puppeteer');
(async () => {
  const browser = await puppeteer.launch({ headless: 'new', args: ['--no-sandbox','--disable-gpu'] });
  const page = await browser.newPage();
  const logs = [];
  page.on('console', msg => logs.push(msg.type() + ': ' + msg.text()));
  page.on('pageerror', err => logs.push('PAGEERR: ' + err.message));
  await page.goto('http://localhost:7777', { waitUntil: 'domcontentloaded', timeout: 15000 });
  await new Promise(r => setTimeout(r, 8000));
  const info = await page.evaluate(() => {
    const dbg = document.getElementById('debug-overlay');
    return {
      debugText: dbg ? dbg.textContent : 'no debug overlay',
    };
  });
  console.log('=== DEBUG OVERLAY ===');
  console.log(info.debugText);
  console.log('=== RELEVANT LOGS ===');
  const relevant = logs.filter(l => /shader|text|font|atlas|FAIL|ERROR|compil|charData|OK|DONE/i.test(l));
  relevant.slice(0, 30).forEach(l => console.log(l.slice(0, 300)));
  console.log('=== ALL ERRORS ===');
  logs.filter(l => l.startsWith('error:') || l.startsWith('PAGEERR:')).forEach(l => console.log(l.slice(0, 300)));
  await browser.close();
})().catch(e => console.log('ERR:', e.message));
