// Dependency-free Chromium capture and browser checks. Node 22+.
import http from 'node:http';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { readFile, writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';

const kit = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.join(kit, 'marketing/screenshots');
let browserPath = process.env.CHROME_PATH;
if (!browserPath) {
  const cache = path.join(process.env.LOCALAPPDATA || '', 'ms-playwright');
  const versions = existsSync(cache) ? (await readdir(cache)).filter(n => /^chromium-\d+$/.test(n)).sort((a,b) => Number(b.split('-')[1])-Number(a.split('-')[1])) : [];
  browserPath = [
    ...versions.map(v => path.join(cache,v,'chrome-win64/chrome.exe')),
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    '/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
  ].find(existsSync);
}
if (!browserPath) throw new Error('Set CHROME_PATH to a Chromium/Chrome/Edge executable.');
const mime = {'.html':'text/html','.js':'text/javascript','.json':'application/json','.css':'text/css','.otf':'font/otf','.svg':'image/svg+xml'};
const server = http.createServer(async (req,res) => {
  try {
    const target = path.resolve(kit, '.' + decodeURIComponent(new URL(req.url,'http://localhost').pathname));
    if (!target.startsWith(kit + path.sep)) { res.writeHead(403).end(); return; }
    res.setHeader('Content-Type',mime[path.extname(target)] || 'application/octet-stream');
    res.end(await readFile(target));
  } catch { res.writeHead(404).end('Not found'); }
});
await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const tempRoot = path.resolve(os.tmpdir());
const userData = await mkdtemp(path.join(tempRoot,'aonik-showcase-'));
const chrome = spawn(browserPath,[ '--headless=new', '--no-first-run', '--no-default-browser-check', '--disable-background-networking', '--remote-debugging-port=0', `--user-data-dir=${userData}`, 'about:blank' ],{windowsHide:true,stdio:['ignore','ignore','pipe']});
let socket;
try {
  const wsUrl = await new Promise((resolve,reject) => {
    const timeout = setTimeout(() => reject(new Error('Chromium launch timed out')),20000);
    let buffer = '';
    chrome.stderr.on('data',data => { buffer+=data; const match=buffer.match(/DevTools listening on (ws:\/\/[^\s]+)/); if(match) { clearTimeout(timeout); resolve(match[1]); } });
    chrome.on('error',reject);
    chrome.on('exit',code => {clearTimeout(timeout);reject(new Error(`Chromium exited ${code}`));});
  });
  socket = new WebSocket(wsUrl);
  await new Promise((resolve,reject) => {socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  let sequence = 0;
  const pending = new Map();
  const errors = [];
  socket.addEventListener('message',event => {
    const data=JSON.parse(event.data);
    if (data.method === 'Runtime.exceptionThrown') errors.push(data.params.exceptionDetails.text + ': ' + (data.params.exceptionDetails.exception?.description || ''));
    if (data.id && pending.has(data.id)) { const {resolve,reject,timer}=pending.get(data.id); clearTimeout(timer);pending.delete(data.id);data.error ? reject(new Error(JSON.stringify(data.error))) : resolve(data.result); }
  });
  const call = (method,params={},sessionId) => new Promise((resolve,reject) => {
    const id=++sequence;
    const timer=setTimeout(() => {pending.delete(id);reject(new Error(`CDP timeout: ${method}`));},20000);
    pending.set(id,{resolve,reject,timer});socket.send(JSON.stringify({id,method,params,...(sessionId?{sessionId}:{})}));
  });
  const {targetId}=await call('Target.createTarget',{url:'about:blank'});
  const {sessionId}=await call('Target.attachToTarget',{targetId,flatten:true});
  const page=(method,params) => call(method,params,sessionId);
  const evaluate=async expression => {
    const result=await page('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});
    if(result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  };
  await page('Page.enable');await page('Runtime.enable');
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  const waitReady=async () => {
    const start=Date.now();
    while(!(await evaluate('window.__showcaseReady === true'))) {
      if(Date.now()-start>15000) throw new Error('Showcase did not render');
      await new Promise(r=>setTimeout(r,100));
    }
    await evaluate('Promise.race([document.fonts.ready, new Promise(resolve => setTimeout(resolve, 8000))]).then(() => true)');
  };
  await mkdir(output,{recursive:true});
  const scenes=[['base','home'],['base','finance.invoices'],['simi','ai.usage'],['food-commerce','commerce.orders'],['food-commerce','commerce.products'],['arke-kids','customers'],['remittance','finance.orders'],['remittance','finance.countries']];
  const report=[];
  for (const [business,screen] of scenes) {
    for (const theme of ['light','dark']) {
      const query=new URLSearchParams({business,screen,theme,capture:'1'});
      await page('Page.navigate',{url:`${origin}/showcase.html?${query}`});
      await waitReady();
      const iconErrors = await evaluate(`[...document.querySelectorAll('nav [data-screen]')].filter(button => {const svg=button.querySelector('svg');return !svg?.innerHTML.trim() || svg.dataset.icon !== AonikMenuIcons.screen(button.dataset.screen,${JSON.stringify(business)});}).map(button=>button.dataset.screen)`);
      if(iconErrors.length) throw new Error(`Unmapped menu icons: ${iconErrors.join(', ')}`);
      const state=await evaluate(`({title:document.title,heading:document.querySelector('h1').textContent,background:getComputedStyle(document.documentElement).getPropertyValue('--background').trim(),primary:getComputedStyle(document.documentElement).getPropertyValue('--primary').trim(),navigation:[...document.querySelectorAll('nav .nav-item span:first-of-type,nav summary > span')].map(e=>e.textContent),rows:document.querySelectorAll('#data-rows tr').length,overflow:document.documentElement.scrollWidth>innerWidth,footerBottom:document.querySelector('.content-footer').getBoundingClientRect().bottom})`);
      if(state.background !== (theme === 'light' ? '#ffffff' : '#1a1d21')) throw new Error('Production background mismatch');
      if(state.primary !== (theme === 'light' ? '#055a60' : '#3a9ea4')) throw new Error('Production primary mismatch');
      if(state.overflow || state.footerBottom>1280) {
        const debug=await page('Page.captureScreenshot',{format:'png'});
        await writeFile(path.join(output,'layout-debug.png'),Buffer.from(debug.data,'base64'));
        const sizes=await evaluate(`[...document.querySelector('.content').children].map(e=>({class:e.className,height:e.getBoundingClientRect().height,top:e.getBoundingClientRect().top}))`);
        throw new Error(`Clipped layout: ${business}/${screen}/${theme}: ${JSON.stringify({state,sizes})}`);
      }
      if(state.navigation.includes('Tenants')) throw new Error('Host menu leaked into tenant scene');
      if(['food-commerce','simi','arke-kids'].includes(business) && state.navigation.includes('Billing')) throw new Error('Finance menu leaked into disabled module');
      if(business==='food-commerce' && !state.navigation.includes('Invoices')) throw new Error('Commerce hard dependency Finance is missing from the food scene');
      if(business==='arke-kids' && state.navigation.includes('Food Orders')) throw new Error('Food menu leaked into Arke Kids');
      const image=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
      const filename=`${business === 'simi' ? 'personal-finance' : business}-${screen.replaceAll('.','-')}-${theme}@2x.png`;
      await writeFile(path.join(output,filename),Buffer.from(image.data,'base64'));
      report.push({file:filename,business,screen,theme,viewport:[1600,1280],pixels:[3200,2560],...state});
      console.log(`Captured ${filename}`);
    }
  }
  // Search, filters, row details, theme toggle, collapse and command palette exercise the real UI.
  await page('Page.navigate',{url:`${origin}/showcase.html?business=base&screen=home`});await waitReady();
  if(!(await evaluate(`(() => { const q=document.querySelector('#record-search');q.value='Cedar';q.dispatchEvent(new Event('input'));return document.querySelectorAll('#data-rows tr').length===1 && document.querySelector('#data-rows').textContent.includes('Cedar'); })()`))) throw new Error('Search failed');
  await evaluate(`document.querySelector('#data-rows tr').click()`);
  if(!(await evaluate(`document.querySelector('dialog').open && document.querySelector('dialog').textContent.includes('PI-DEMO-1000')`))) throw new Error('Order reference drawer failed');
  await evaluate(`document.querySelector('dialog').close();document.querySelector('[data-action="theme"]').click()`);
  if(await evaluate(`document.documentElement.dataset.theme`) !== 'dark') throw new Error('Theme toggle failed');
  await evaluate(`document.querySelector('[data-action="collapse"]').click()`);
  if(await evaluate(`Math.round(document.querySelector('.sidebar').getBoundingClientRect().width)`) !== 56) throw new Error('Sidebar collapse failed');
  await evaluate(`document.querySelector('[data-action="palette"]').click()`);
  if(!(await evaluate(`document.querySelector('dialog').open && document.querySelectorAll('[data-destination]').length>10`))) throw new Error('Palette failed');
  await evaluate(`document.querySelector('dialog').close();document.querySelector('[data-action="filter"]').click()`);
  if(!(await evaluate(`document.querySelector('[data-action="filter"]').getAttribute('aria-pressed')==='true'`))) throw new Error('Status filter failed');
  // Invalid routes and unknown types cannot reveal screens outside the resolved profile.
  await page('Page.navigate',{url:`${origin}/showcase.html?business=arke-kids&screen=finance.invoices`});await waitReady();
  if(await evaluate(`document.querySelector('h1').textContent`) !== 'Customers') throw new Error('Disabled screen fallback failed');
  await page('Emulation.setDeviceMetricsOverride',{width:1024,height:900,deviceScaleFactor:1,mobile:false});
  if(await evaluate('document.documentElement.scrollWidth>innerWidth')) throw new Error('1024px layout overflows');
  await page('Page.navigate',{url:`${origin}/marketing/index.html`});
  const galleryStart=Date.now();
  while(await evaluate(`document.querySelectorAll('#gallery article').length`) !== 11) {
    if(Date.now()-galleryStart>10000) throw new Error('Gallery did not render');
    await new Promise(r=>setTimeout(r,100));
  }
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1800,deviceScaleFactor:2,mobile:false});
  for(const theme of ['light','dark']) {
    await page('Page.navigate',{url:`${origin}/menu-icons.html?theme=${theme}`});
    const iconStart=Date.now();
    while(!(await evaluate(`document.querySelectorAll('.icons article').length>0 && [...document.querySelectorAll('.icons article')].every(article=>article.querySelectorAll('svg').length===2)`))) {
      if(Date.now()-iconStart>10000) throw new Error('Icon sheet did not render');
      await new Promise(r=>setTimeout(r,100));
    }
    await evaluate('Promise.race([document.fonts.ready, new Promise(resolve => setTimeout(resolve, 8000))]).then(() => true)');
    const invalidGlyphs=await evaluate(`[...document.querySelectorAll('.icons article svg')].filter(svg=>{const b=svg.getBBox();return b.width<=0 || b.height<=0 || b.x<.8 || b.y<.8 || b.x+b.width>23.2 || b.y+b.height>23.2;}).map(svg=>svg.closest('article').textContent.trim())`);
    if(invalidGlyphs.length) throw new Error(`Clipped or empty icon geometry: ${invalidGlyphs.join(', ')}`);
    const sheet=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});
    await writeFile(path.join(output,`menu-icons-${theme}@2x.png`),Buffer.from(sheet.data,'base64'));
    console.log(`Captured menu-icons-${theme}@2x.png`);
  }
  if(process.argv.includes('--canvas')) {
    await page('Page.navigate',{url:`${origin}/index.html`});
    const canvasStart=Date.now();
    while(!(await evaluate(`document.querySelector('#root')?.children.length>0`))) {
      if(Date.now()-canvasStart>45000) throw new Error('Original design canvas did not render; check its React/Babel CDN access.');
      await new Promise(r=>setTimeout(r,250));
    }
    console.log('Original design canvas rendered successfully.');
  }
  if(errors.length) throw new Error(errors.join('\n'));
  await writeFile(path.join(output,'capture-report.json'),JSON.stringify({checks:'Passed: mapped menu icons, icon sheet geometry, theme tokens, per-profile menus, no host menus, no horizontal overflow, complete 1600×1280 layouts, search, row references, theme toggle, sidebar collapse, palette, status filter, forbidden-route fallback, 1024px layout, no runtime exceptions.',scenes:report},null,2)+'\n');
  console.log(`Browser checks passed. ${report.length} scene PNGs and 2 icon sheets saved to ${output}`);
} finally {
  socket?.close();
  chrome.kill();
  await new Promise(resolve => chrome.exitCode !== null ? resolve() : chrome.once('exit',resolve));
  server.close();
  // Only remove the unique temporary profile created by this invocation, inside the OS temp folder.
  const resolved=path.resolve(userData);
  if(path.dirname(resolved) === tempRoot && path.basename(resolved).startsWith('aonik-showcase-')) {
    await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
  }
}
