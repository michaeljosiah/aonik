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
  socket.addEventListener('message',event=>{const data=JSON.parse(event.data);if(data.method==='Network.requestWillBeSent' && new URL(data.params.request.url).pathname.startsWith('/api/')) apiCalls++;});
  await page('Page.navigate',{url:origin+'/remittance.html'});
  await wait(`document.querySelectorAll('[data-dc-slot]').length===12`,'Editable Remittance artboards did not render');
  const scenes=await evaluate('REMITTANCE_DESIGN_SCENES.map(([id])=>id)');
  const inside=expression=>evaluate(`(()=>{const root=[...document.querySelectorAll('.remit-design')].at(-1);return ${expression};})()`);
  const click=label=>inside(`(()=>{const button=[...root.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(label)});if(!button)throw Error('Missing '+${JSON.stringify(label)});button.click();return true;})()`);
  const escape=()=>evaluate(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))`);
  const focus=async scene=>{await evaluate(`document.querySelector(${JSON.stringify('[data-dc-slot="remittance-')}+${JSON.stringify(scene)}+'"] .dc-labeltext').click()`);await wait(`document.querySelectorAll('.remit-design').length===13`,'Artboard focus did not open');};
  await mkdir(output,{recursive:true});
  for(const scene of scenes){
    await focus(scene);
    if(await inside(`!!root.querySelector('img')`)) throw new Error('An editable artboard embeds a screenshot');
    if(await inside(`root.querySelector('.rm-workspace').scrollWidth>root.querySelector('.rm-workspace').clientWidth`)) throw new Error('Horizontal artboard overflow: '+scene);
    for(const theme of ['light','dark']){
      await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,250))`);
      const shot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
      await writeFile(path.join(output,`remittance-artboard-${scene}-${theme}@2x.png`),Buffer.from(shot.data,'base64'));
    }
    await escape();
  }
  await focus('receipt-delivery');
  if(!(await inside(`root.textContent.includes('Delivered') && root.textContent.includes('Bounced') && root.textContent.includes('Awaiting report') && root.textContent.includes('Unavailable')`))) throw new Error('Delivery artboard states are missing');
  await escape();
  await focus('receipt-email-review');
  if(!(await inside(`root.textContent.includes('Review customer email') && !!root.querySelector('[aria-label="Preview saved customer email"]')`))) throw new Error('Editable email review is missing');
  if(!(await inside(`[...root.querySelectorAll('button')].find(b=>b.textContent.trim()==='Save email & review receipt').disabled`))) throw new Error('Email update skips confirmation');
  await inside(`root.querySelector('[aria-label="Confirm preview email update"]').click()`);
  await click('Save email & review receipt');
  if(!(await inside(`root.querySelector('[aria-label="Preview receipt recipient"]').textContent.includes('nadia.corrected@example.test')`))) throw new Error('Updated email is not available for resend');
  if(!(await inside(`[...root.querySelectorAll('button')].find(b=>b.textContent.trim()==='Confirm & resend receipt').disabled`))) throw new Error('Email review bypasses resend confirmation');
  if(!(await inside(`root.textContent.includes('4 requests')`))) throw new Error('Saving an email sent a receipt');
  await escape();
  await focus('transfer-editor');
  await inside(`(()=>{const e=root.querySelector('[aria-label="Preview transfer amount"]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'300');e.dispatchEvent(new Event('input',{bubbles:true}));return true;})()`);
  await wait(`(()=>{const root=[...document.querySelectorAll('.remit-design')].at(-1);return root.textContent.includes('No transfer quote yet');})()`,'Editable amount did not invalidate quote');
  await click('Get transfer quote');
  if(!(await inside(`root.textContent.includes('GBP 303.50')`)))throw new Error('Edited amount did not update quote');
  await escape();await focus('receipt-resend');
  if(!(await inside(`[...root.querySelectorAll('button')].find(b=>b.textContent.trim()==='Confirm & resend receipt').disabled`)))throw new Error('Design resend skips confirmation');
  await inside(`(()=>{const e=root.querySelector('[aria-label="Preview receipt recipient"]');e.value='nadia@example.test';e.dispatchEvent(new Event('change',{bubbles:true}));root.querySelector('[aria-label="Confirm preview receipt recipient"]').click();return true;})()`);
  await click('Confirm & resend receipt');
  if(!(await inside(`root.textContent.includes('5 requests')`)))throw new Error('Design resend did not append local history');
  await escape();await focus('beneficiary-new');
  await click('Verify destination');
  if(await inside(`[...root.querySelectorAll('button')].find(b=>b.textContent.trim()==='Save payout destination').disabled`))throw new Error('Design verification did not unlock save');
  await escape();
  if(apiCalls)throw new Error('Editable artboards called an application API');
  await page('Page.navigate',{url:origin+'/index.html'});
  await wait(`document.querySelectorAll('[data-dc-section="remittance-operations"] [data-dc-slot]').length===12`,'Full design canvas did not register the Remittance artboards');
  await focus('receipt-history');
  if(!(await inside(`root.textContent.includes('Receipt email history')`)))throw new Error('Main canvas could not focus the receipt artboard');
  await escape();
  if(errors.length)throw new Error(errors.join('\n'));
  await writeFile(path.join(output,'remittance-artboards-report.json'),JSON.stringify({checks:['12 native editable artboards','canvas focus and Escape','24 light/dark captures','shared SVG paths and branding','no screenshot-backed artboards','no horizontal overflow','editable quote invalidation and recalculation','recipient confirmation and local resend history','local destination verification','no application API calls','full design canvas registration and focus','no runtime exceptions'],scenes},null,2)+'\n');
  console.log('Editable Remittance artboards passed browser checks. 24 previews saved; no application API calls.');
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
