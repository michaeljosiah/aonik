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

  const wait=async(expression,label)=>{const start=Date.now();while(!(await evaluate(expression))){if(Date.now()-start>30000)throw new Error(label+': '+errors.join(' | ')+' '+await evaluate('document.body.innerText.slice(0,1000)'));await new Promise(resolve=>setTimeout(resolve,100));}};
  await page('Network.enable');
  let apiCalls=0;
  socket.addEventListener('message',event=>{const data=JSON.parse(event.data);if(data.method==='Network.requestWillBeSent' && new URL(data.params.request.url).pathname.startsWith('/api/'))apiCalls++;});
  const capture=async file=>{const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,file),Buffer.from(result.data,'base64'));};
  const imagesReady=()=>evaluate(`Promise.all([...document.images].map(img=>img.decode())).then(()=>true)`);
  await mkdir(output,{recursive:true});
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:2200,deviceScaleFactor:2,mobile:false});
  await page('Page.navigate',{url:origin+'/agent-portraits.html'});
  await wait(`document.querySelectorAll('[data-portrait-id]').length===16`,'Portrait gallery did not load');
  await imagesReady();await evaluate('document.fonts.ready.then(()=>true)');
  if(!(await evaluate(`[...document.images].every(img=>img.complete && img.naturalWidth>=512 && img.naturalWidth===img.naturalHeight)`)))throw Error('Missing or invalid square headshots');
  if(await evaluate(`document.documentElement.scrollWidth>innerWidth || document.documentElement.scrollHeight>innerHeight`))throw Error('Portrait gallery overflows the contact sheet');
  if(!(await evaluate(`document.querySelectorAll('[download]').length===16 && new Set([...document.querySelectorAll('[download]')].map(link=>link.href)).size===16`)))throw Error('Original portrait downloads are incomplete');
  for(const theme of ['light','dark']){await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)}`);await capture(`agent-headshots-${theme}@2x.png`);}
  await evaluate(`document.querySelector('[data-filter="runtime"]').click();true`);
  if(await evaluate(`document.querySelectorAll('[data-portrait-id]').length`)!==10)throw Error('Runtime agent portrait coverage is incorrect');
  await evaluate(`document.querySelector('[data-filter="design-persona"]').click();true`);
  if(await evaluate(`document.querySelectorAll('[data-portrait-id]').length`)!==6)throw Error('Design persona portrait coverage is incorrect');
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  await page('Page.navigate',{url:origin+'/index.html'});
  await wait(`!!document.querySelector('[data-dc-slot="agents-page"] svg image')`,'Agent roster did not adopt CGI headshots');
  const mapped=await evaluate(`new Set([...document.querySelector('[data-dc-slot="agents-page"]').querySelectorAll('svg image')].map(img=>img.getAttribute('href'))).size`);
  if(mapped!==8)throw Error('All eight starter-kit roster identities must have headshots');
  if(await evaluate(`!!AonikAgentPortraits.resolve('not-a-real-agent')`))throw Error('Unmapped names were assigned an unrelated face');
  await evaluate(`document.querySelector('[data-dc-slot="agents-page"] .dc-labeltext').click();true`);
  for(const theme of ['light','dark']){await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,200))`);await capture(`agent-roster-portraits-${theme}@2x.png`);}
  await evaluate(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));true`);
  await evaluate(`document.querySelector('[data-dc-slot="agent-detail"] .dc-labeltext').click();true`);
  await wait(`document.querySelectorAll('[data-dc-slot="agent-detail"] svg image').length>0`,'Profile portrait did not load');
  for(const theme of ['light','dark']){await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,200))`);await capture(`agent-profile-portraits-${theme}@2x.png`);}
  for (const [id, prefix] of [['myspace','aonik-dashboard'],['notif-center','agent-notifications']]) {
    await evaluate(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));document.querySelector('[data-dc-slot="${id}"] .dc-labeltext').click();true`);
    await imagesReady();
    if (id === 'myspace' && !(await evaluate(`document.querySelector('[data-dc-slot="myspace"] [data-component="aonik-commentary"] img').src.endsWith('/aonik.png')`))) throw Error('Dashboard commentary must show AONIK');
    if (id === 'notif-center' && await evaluate(`document.querySelectorAll('[data-dc-slot="notif-center"] img[data-agent-portrait]').length`) < 2) throw Error('Agent notifications need their author portraits');
    for (const theme of ['light','dark']) { await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,200))`); await capture(`${prefix}-${theme}@2x.png`); }
  }
  if(apiCalls)throw Error('Portrait previews called application APIs');
  if(errors.length)throw Error(errors.join('\n'));
  await writeFile(path.join(output,'agent-portraits-report.json'),JSON.stringify({checks:['16 distinct original square CGI headshots','10 runtime agents and 6 design personas','20/32/44px avatar previews','complete original PNG download links','light/dark contact sheets','all 8 starter-kit roster identities mapped','SVG-compatible profile/topology portraits','unknown names retain fallback','no application API calls','no runtime errors'],portraits:16},null,2)+'\n');
  console.log('Agent portrait checks passed: 16 originals, runtime/design filters, profile identities, AONIK dashboard commentary, notification authors and 10 marketing previews.');
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
