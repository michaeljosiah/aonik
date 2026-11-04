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
  const nav=async scene=>{await page('Page.navigate',{url:origin+'/arke-worlds.html?capture=1&scene='+scene});await wait(`!!document.querySelector('[data-arke-scene="${scene}"]')`,'Arke scene did not load: '+scene);await evaluate('document.fonts.ready.then(()=>true)');await evaluate(`Promise.all([...document.querySelectorAll(".aw-design img")].map(img=>img.decode())).then(()=>true)`);};
  const click=label=>evaluate(`(()=>{const scope=document.querySelector('.fd-dialog')||document.querySelector('.aw-design');const b=[...scope.querySelectorAll('button')].find(b=>(b.textContent.trim()===${JSON.stringify(label)}||b.getAttribute('aria-label')===${JSON.stringify(label)}));if(!b)throw Error('Missing button '+${JSON.stringify(label)});b.click();return true;})()`);
  const aria=label=>evaluate(`(()=>{const e=document.querySelector('[aria-label="'+${JSON.stringify(label)}+'"]');if(!e)throw Error('Missing '+${JSON.stringify(label)});e.click();return true;})()`);
  const input=async(selector,value)=>evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});Object.getOwnPropertyDescriptor(e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));return true;})()`);
  const select=async(label,value)=>evaluate(`(()=>{const e=document.querySelector('[aria-label="'+${JSON.stringify(label)}+'"]');e.value=${JSON.stringify(value)};e.dispatchEvent(new Event('change',{bubbles:true}));return true;})()`);
  const shot=async file=>{const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,file),Buffer.from(result.data,'base64'));};
  const capture=async prefix=>{let light;for(const theme of ['light','dark']){const bg=await evaluate(`(async()=>{document.documentElement.dataset.theme=${JSON.stringify(theme)};await new Promise(resolve=>setTimeout(resolve,350));return getComputedStyle(document.documentElement).getPropertyValue('--background');})()`);if(theme==='light')light=bg;else if(bg===light)throw Error('Food themes did not differ');await shot(`${prefix}-${theme}@2x.png`);}};
  await mkdir(output,{recursive:true});
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1100,deviceScaleFactor:2,mobile:false});
  await nav('overview');
  const scenes=await evaluate('AW_SCENES.map(s=>({id:s.id,title:s.title}))');
  if(scenes.length!==28)throw Error('Incomplete Arke Worlds inventory');
  for(const {id} of (process.env.ARKE_SKIP_CAPTURES?[]:scenes)){
    await nav(id);
    const overflow=await evaluate(`[...document.querySelectorAll('.aw-design,.fd-main,.fd-content,.fd-table-wrap')].filter(e=>e.scrollWidth>e.clientWidth+1).map(e=>e.className)`);
    if(overflow.length)throw Error('Overflow in '+id+': '+overflow.join(', '));
    if(!['submissions','submission','catalogue'].includes(id)&&await evaluate(`document.querySelectorAll('.aw-design .fd-primary').length`)!==1)throw Error('Competing primary actions in '+id);
    await capture('arke-worlds-'+id);
  }
  await nav('customers');await aria('Open CUS-AW-102');await aria('Family Billing tab');
  if(!(await evaluate(`document.querySelector('.fd-content').textContent.includes('Shah family') && !document.querySelector('.fd-content').textContent.includes('View invoice') && !document.querySelector('.fd-content').textContent.includes('Credit transactions')`)))throw Error('Family billing exposed another family');
  await nav('worlds');await aria('Open WRL-101');await click('Review an edition');
  if(!(await evaluate(`document.querySelector('[data-arke-scene=edition]').textContent.includes('A Light for the Garden')`)))throw Error('World lost its edition identity');
  await click('Review delivery');await aria('All 12 pages and illustrations reviewed');await aria('Narration reviewed from start to finish');await aria('Guardian and private delivery recipient verified');await click('Approve private delivery');await click('Print this edition');await click('Review print request');await aria('Confirm exact print proof reviewed');await aria('Confirm guardian authorization and delivery address');await click('Save reviewed print request');
  if(!(await evaluate(`document.querySelector('[data-arke-scene=print]').textContent.includes('Awaiting execution') && document.body.innerText.includes('separately quoted')`)))throw Error('Print request skipped reviewed intent');
  await nav('print');await click('Review print request');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(b=>b.textContent==='Save reviewed print request').disabled`)))throw Error('Unapproved edition can print');await aria('Close Arke review');
  await nav('job');await click('Resume delivery');await aria('Confirm saved-output delivery retry');await click('Resume saved delivery');await click('Inspect credits');
  if(!(await evaluate(`document.querySelector('.fd-metrics').textContent.includes('90 credits') && document.querySelector('.fd-metrics').textContent.includes('20 credits') && document.querySelector('.fd-metrics').textContent.includes('5 credits')`)))throw Error('Delivery recovery changed the balances');
  await nav('auto-stories');await click('Review monthly episode');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(b=>b.textContent==='Preview one episode allocation').disabled`)))throw Error('Unresolved offer allowed monthly allocation');await aria('Close Arke review');await aria('Preview approved v3 policy');await click('Review monthly episode');await aria('Confirm active child, guardian permission and this unused cycle');await click('Preview one episode allocation');await click('Review monthly episode');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(b=>b.textContent==='Cycle already used').disabled`)))throw Error('Monthly slot reused');await aria('Close Arke review');
  await nav('plans');
  if(!(await evaluate(`document.body.innerText.includes('not an entitlement migration') && document.body.innerText.includes('$24.99') && document.body.innerText.includes('not the Arke Studio desktop product')`)))throw Error('Commercial proposal ambiguity was hidden');
  await nav('safety');await select('Safety review modality','Voice reference');await click('Review held output');await aria('Confirm evidence and permitted scope');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(b=>b.textContent==='Record safety review').disabled`)))throw Error('Unsupported voice rights/qualification bypass');await aria('Close Arke review');
  // Publishing controls are exercised by verify-publishing.mjs, using the exact shared view.
  await nav('overview');
  await select('Arke design role','editor');
  if(!(await evaluate(`![...document.querySelectorAll('[aria-label="Arke Worlds navigation"] button')].some(b=>['People','Billing','Orders & print'].includes(b.textContent.trim()))`)))throw Error('Editorial role includes child or financial operations');
  await select('Arke design role','operator');await select('Arke product view','creator');await click('Worlds & stories');
  if(!(await evaluate(`document.querySelectorAll('[data-arke-row]').length===1 && document.querySelector('[data-arke-row]').textContent.includes('WRL-201')`)))throw Error('Creator lens contains private family worlds');
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  for(const id of ['overview','family','submission','plans']){
    await page('Page.navigate',{url:origin+'/arke-worlds.html?scene='+id});
    await wait(`document.querySelectorAll('[data-arke-scene="${id}"]').length===2`,'Arke editable artboard deep link failed: '+id);await capture('arke-worlds-artboard-'+id);
  }
  await page('Page.navigate',{url:origin+'/index.html'});await wait(`document.querySelectorAll('[data-arke-scene]').length===28`,'Main Arke canvas missing');
  if(apiCalls)throw Error('Design called application APIs');if(errors.length)throw Error(errors.join(' | '));
  await writeFile(path.join(output,'arke-worlds-design-report.json'),JSON.stringify({scenes,checks:['28 native artboards in light/dark','one primary page action','family and product scoping','world-to-edition identity','parent and proof checks precede print intent','no payment or partner execution','saved delivery recovery leaves usage unchanged','unresolved commercial offer blocks monthly programme','per-child monthly slot consumed once without flex credits','voice qualification and rights cannot be bypassed','publishing views delegate to shared tested canvas','editor role omits family and billing operations','creator product lens excludes private family worlds','main canvas and four focused artboards','no API calls or runtime exceptions']},null,2)+'\n');
  console.log('Arke Worlds design checks passed: 28 editable artboards, 64 theme exports and connected review flows.');
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
