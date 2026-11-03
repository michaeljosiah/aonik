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
  const nav=async scene=>{await page('Page.navigate',{url:origin+'/support.html?capture=1&scene='+scene});await wait(`!!document.querySelector('[data-support-scene="${scene}"]')`,'Support scene did not load: '+scene);await evaluate('document.fonts.ready.then(()=>true)');await evaluate(`Promise.all([...document.querySelectorAll(".support-design img")].map(img=>img.decode())).then(()=>true)`);};
  const click=label=>evaluate(`(()=>{const b=[...document.querySelectorAll('.support-design button')].find(b=>b.textContent.trim()===${JSON.stringify(label)});if(!b)throw Error('Missing button '+${JSON.stringify(label)});b.click();return true;})()`);
  const aria=label=>evaluate(`(()=>{const e=document.querySelector('[aria-label="'+${JSON.stringify(label)}+'"]');if(!e)throw Error('Missing '+${JSON.stringify(label)});e.click();return true;})()`);
  const input=async(selector,value)=>evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});Object.getOwnPropertyDescriptor(e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));return true;})()`);
  const shot=async file=>{const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,file),Buffer.from(result.data,'base64'));};
  const capture=async prefix=>{let light;for(const theme of ['light','dark']){const bg=await evaluate(`(async()=>{document.documentElement.dataset.theme=${JSON.stringify(theme)};await new Promise(resolve=>setTimeout(resolve,400));return getComputedStyle(document.documentElement).getPropertyValue('--background');})()`);if(theme==='light')light=bg;else if(bg===light)throw Error('Dark support theme did not change');await shot(`${prefix}-${theme}@2x.png`);}};
  await mkdir(output,{recursive:true});
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1100,deviceScaleFactor:2,mobile:false});
  const scenes=['inbox','conversation','reply-review','escalations'];
  for(const scene of scenes){
    await nav(scene);
    const overflow=await evaluate(`[...document.querySelectorAll('.support-design,.sp-main,.sp-thread,.sp-copilot,.sp-review-editor,.sp-escalation-main')].filter(e=>e.scrollWidth>e.clientWidth+1).map(e=>e.className)`);
    if(overflow.length)throw Error('Horizontal overflow in '+scene+': '+overflow.join(', '));
    if(await evaluate(`!!document.querySelector('.support-design img:not([data-agent-portrait])')`))throw Error('Support surface contains screenshot-backed content');
    if(!(await evaluate(`document.querySelectorAll('.support-design [data-icon^="menu-"]').length>10`)))throw Error('Shared support icons are missing');
    await capture('support-'+scene);
  }
  await nav('inbox');
  if(await evaluate(`document.querySelectorAll('[data-support-case]').length`)!==6)throw Error('Populated inbox cases are missing');
  await input('[aria-label="Search support inbox"]','bank');
  if(await evaluate(`document.querySelectorAll('[data-support-case]').length`)!==1)throw Error('Inbox search did not filter');
  await evaluate(`document.querySelector('[data-support-case="CS-1037"]').click();true`);await click('Review AI draft');
  if(!(await evaluate(`document.querySelector('[aria-label="Review email reply"]').value.includes('Hi Alex') && !document.querySelector('[aria-label="Review email reply"]').value.includes('Nadia')`)))throw Error('Draft leaked another customer context');
  await nav('inbox');await click('Personal Finance');
  if(await evaluate(`document.querySelectorAll('[data-support-case]').length`)!==2)throw Error('Business inbox filter failed');
  await nav('inbox');await click('Review suggestion');await click('Cancel');
  if(!(await evaluate(`document.querySelector('.sp-detail-list').textContent.includes('High') && document.querySelector('[data-support-case="CS-1042"]').textContent.includes('Needs reply')`)))throw Error('Opening triage review applied the suggestion');
  await click('Review suggestion');await click('Apply suggestion');
  if(!(await evaluate(`document.querySelector('[data-support-case="CS-1042"]').textContent.includes('High priority')`)))throw Error('Triage confirmation did not apply');
  await aria('Open full conversation');
  await click('Internal note');await input('[aria-label="Internal support note"]','Please check the partner status before promising a delivery time.');await click('Add note');
  if(!(await evaluate(`document.querySelector('.sp-thread-scroll').textContent.includes('Please check the partner status before promising a delivery time.')`)))throw Error('Internal note did not append');
  await click('Review AI draft');await wait(`!!document.querySelector('[aria-label="Review email reply"]')`,'Draft review did not open');
  await input('[aria-label="Review email reply"]','');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Review and send').disabled`)))throw Error('Empty draft remains sendable');
  await input('[aria-label="Review email reply"]','Hi Nadia, we are checking the recorded payout status. I will update you by 14:30 UTC.');
  await click('Review and send');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Send reply').disabled`)))throw Error('Reply skips recipient and content confirmation');
  await capture('support-send-confirmation');await click('Keep editing');
  if(await evaluate(`document.body.innerText.includes('Reply queued for Nadia')`))throw Error('Opening a send confirmation queued the reply');
  await click('Review and send');await aria('Confirm reply recipient and content');await click('Send reply');
  if(!(await evaluate(`document.querySelector('.sp-thread-scroll').textContent.includes('Queued for delivery') && document.body.innerText.includes('Delivery is pending.')`)))throw Error('Local queue state is missing or claims delivery');
  await nav('reply-review');await evaluate(`document.querySelector('.sp-review-evidence .sp-evidence-list button').click();true`);
  if(!(await evaluate(`document.querySelector('[role="dialog"]').textContent.includes('no completed payout is recorded')`)))throw Error('Source evidence failed to distinguish payout status');
  await click('Close');
  await nav('escalations');await click('Review handoff');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Request handoff').disabled`)))throw Error('Operational handoff skips review');
  await capture('support-handoff-confirmation');await click('Cancel');
  if(await evaluate(`document.body.innerText.includes('Handoff requested by')`))throw Error('Opening a handoff requested it');
  await click('Review handoff');await aria('Confirm support handoff');await click('Request handoff');
  if(!(await evaluate(`document.body.innerText.includes('Handoff requested by Oliver Reed')`)))throw Error('Reviewed handoff did not update local state');
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  await page('Page.navigate',{url:origin+'/support.html'});
  await wait(`document.querySelectorAll('[data-dc-slot]').length===4`,'Editable support artboards did not register');
  for(const scene of scenes){
    await evaluate(`document.querySelector('[data-dc-slot="support-${scene}"] .dc-labeltext').click();true`);
    await wait(`document.querySelectorAll('.support-design').length===5`,'Support artboard focus did not open');
    await capture('support-artboard-'+scene);
    await evaluate(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));true`);
  }
  await page('Page.navigate',{url:origin+'/index.html'});
  await wait(`document.querySelectorAll('[data-dc-section="support"] [data-dc-slot]').length===4`,'Main canvas support registration is missing');
  await evaluate(`document.querySelector('[data-dc-slot="support-inbox"] .dc-labeltext').click();true`);
  await wait(`document.querySelectorAll('.support-design').length===5`,'Main canvas support focus failed');
  if(apiCalls)throw Error('Support designs called application APIs');
  if(errors.length)throw Error(errors.join('\n'));
  await writeFile(path.join(output,'support-design-report.json'),JSON.stringify({checks:['4 native editable support artboards','8 clean scene exports','4 confirmation-state exports','8 editable canvas captures','light/dark semantic tokens','approved AONIK portrait and canonical SVG icons','search and business filters','case-specific drafts without cross-customer context','reviewed triage suggestions','internal team notes','editable reply with empty-draft guard','recipient and exact-content confirmation','queue state without claiming delivery','linked sources distinguish funding from payout','reviewed operational handoffs','focused and main canvas registrations','no application API calls or runtime exceptions'],scenes},null,2)+'\n');
  console.log('Support design checks passed: 4 editable artboards, 20 light/dark captures, no application API calls.');
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
