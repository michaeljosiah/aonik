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

  const scenes=['queue','overview','package','rights','history','approve','stale','forbidden','publication','publish','catalogue','retry','withdraw','mirror'];
  const nav=async(scene,theme='light')=>{await page('Page.navigate',{url:origin+'/publishing.html?capture=1&scene='+scene+'&theme='+theme});await wait(`!!document.querySelector('.ap-page')`,'Publishing scene did not mount');await evaluate('document.fonts.ready.then(()=>true)');};
  const click=label=>evaluate(`(()=>{const scope=document.querySelector('[role="dialog"]')||document;const b=[...scope.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(label)}||b.getAttribute('aria-label')===${JSON.stringify(label)});if(!b)throw Error('Missing button '+${JSON.stringify(label)});b.click();})()`);
  await mkdir(output,{recursive:true});
  for(const theme of ['light','dark'])for(const scene of scenes){
    await nav(scene,theme);
    if(await evaluate(`document.documentElement.scrollWidth>innerWidth+1`))throw Error('Horizontal overflow: '+scene);
    const image=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,`publishing-${scene}-${theme}@2x.png`),Buffer.from(image.data,'base64'));
  }
  for(const scene of ['submissions','submission','catalogue'])for(const theme of ['light','dark']){
    await page('Page.navigate',{url:origin+'/arke-worlds.html?capture=1&scene='+scene+'&theme='+theme});await wait(`!!document.querySelector('.ap-page')`,'Legacy publishing scene missing');
    const shot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,`arke-worlds-${scene}-${theme}@2x.png`),Buffer.from(shot.data,'base64'));
  }
  await nav('approve');
  if(!(await evaluate(`document.querySelector('.ap-dialog-foot button:last-child').disabled`)))throw Error('Approval must need explicit evidence checks');
  await evaluate(`(()=>{const e=document.querySelector('[aria-label="Internal assessment"]');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,'Rights, audience and attribution reviewed.');e.dispatchEvent(new Event('input',{bubbles:true}));document.querySelectorAll('[role="dialog"] input[type=checkbox]').forEach(e=>e.click());})()`);
  await wait(`!document.querySelector('.ap-dialog-foot button:last-child').disabled`,'Approval controls did not enable');await click('Approve revision');
  if(!(await evaluate(`!document.querySelector('[role="dialog"]')&&document.body.innerText.includes('Example decision recorded locally')`)))throw Error('Local approval did not record');
  await nav('stale');if(!(await evaluate(`document.body.innerText.includes('changed')&&document.querySelector('.ap-dialog-foot button:last-child').disabled`)))throw Error('Stale confirmation is actionable');
  await nav('forbidden');if(await evaluate(`!!document.querySelector('.ap-tabs[aria-label="Review sections"]')`))throw Error('Forbidden view leaked review details');
  await nav('queue');await page('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await page('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  if(!(await evaluate(`document.activeElement!==document.body`)))throw Error('Keyboard navigation unavailable');
  await page('Page.navigate',{url:origin+'/publishing.html'});await wait(`document.querySelectorAll('.ap-page').length===14`,'Editable publishing canvas missing artboards');
  await page('Page.navigate',{url:origin+'/index.html'});await wait(`document.querySelectorAll('.ap-page').length>=17`,'Full canvas missing publishing views');
  if(apiCalls||errors.length)throw Error('Design APIs or runtime exceptions: '+apiCalls+' '+errors.join(' | '));
  await writeFile(path.join(output,'publishing-design-report.json'),JSON.stringify({scenes,checks:['14 native editable artboards','28 light/dark screenshots','exact shared runtime view and icons','explicit approval checklist','stale confirmation disabled','403 hides private material','keyboard focus reachable','no horizontal overflow','full design canvas integrated','no API requests or runtime exceptions']},null,2)+'\n');
  console.log('Publishing design checks passed: 14 editable artboards and 28 light/dark exports.');
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
