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
  const nav=async scene=>{await page('Page.navigate',{url:origin+'/chat.html?capture=1&scene='+scene});await wait(`!!document.querySelector('[data-chat-scene="${scene}"]')`,'Chat scene did not load: '+scene);await evaluate('document.fonts.ready.then(()=>true)');await evaluate(`Promise.all([...document.querySelectorAll(".chat-design img")].map(img=>img.decode())).then(()=>true)`);};
  const click=label=>evaluate(`(()=>{const b=[...document.querySelectorAll('.chat-design button')].find(b=>b.textContent.trim()===${JSON.stringify(label)});if(!b)throw Error('Missing button '+${JSON.stringify(label)});b.click();return true;})()`);
  const aria=label=>evaluate(`(()=>{const e=document.querySelector(${JSON.stringify('[aria-label="')}+${JSON.stringify(label)}+'"]');if(!e)throw Error('Missing '+${JSON.stringify(label)});e.click();return true;})()`);
  const input=async(selector,value)=>evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});Object.getOwnPropertyDescriptor(e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));return true;})()`);
  const shot=async file=>{const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,file),Buffer.from(result.data,'base64'));};
  await mkdir(output,{recursive:true});
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1100,deviceScaleFactor:2,mobile:false});
  const scenes=['slideout','permissions','collaboration','proposal-review','notifications','mentions'];
  for(const scene of scenes){
    await nav(scene);
    const overflow=await evaluate(`[...document.querySelectorAll('.chat-design,.cc-app-body,.cc-drawer,.cc-conversation,.cc-dialog')].filter(e=>e.scrollWidth>e.clientWidth+1).map(e=>e.className)`);
    if(overflow.length)throw Error('Horizontal overflow in '+scene+': '+overflow.join(', '));
    if(await evaluate(`!!document.querySelector('.chat-design img:not([data-agent-portrait])')`))throw Error('Screenshot-backed chat surface');
    if(!(await evaluate(`document.querySelectorAll('.chat-design [data-icon^="menu-"]').length>15`)))throw Error('Shared icons are missing');
    for(const theme of ['light','dark']){await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,200))`);await shot(`chat-${scene}-${theme}@2x.png`);}
  }
  await nav('notifications');
  if(!(await evaluate(`document.querySelectorAll('.cc-inbox [data-agent-portrait]').length===2`)))throw Error('Notification agent portraits missing');
  await click('Mentions');
  if(await evaluate(`document.querySelectorAll('[data-design-notification]').length`)!==2)throw Error('Mentions filter failed');
  await evaluate(`document.querySelector('[data-design-notification="mention-maria"] button').click();true`);
  if(!(await evaluate(`!document.querySelector('.cc-inbox') && !!document.querySelector('.cc-notification-target')`)))throw Error('Notification did not open linked message');
  await aria('Open chat notifications');await click('Unread');
  if(await evaluate(`document.querySelectorAll('[data-design-notification]').length`)!==2)throw Error('Opening did not mark the mention read');
  await click('Mark all read');
  if(!(await evaluate(`document.body.innerText.includes('You are all caught up.')`)))throw Error('Mark all read failed');
  await nav('mentions');await click('Dismiss');
  if(await evaluate(`document.querySelectorAll('[data-design-notification]').length`)!==1)throw Error('Dismiss failed');
  await nav('slideout');
  if(!(await evaluate(`document.body.innerText.includes('GBP 4,250 remains due.') && document.body.innerText.includes('JRN-8821')`)))throw Error('Evidence-backed example response is missing');
  await click('Approve once');
  if(!(await evaluate(`document.querySelector('[aria-label="Draft invoice permission request"]').textContent.includes('Approved once')`)))throw Error('One-time permission did not resolve');
  if(await evaluate(`!![...document.querySelectorAll('.chat-design button')].find(b=>b.textContent.trim()==='Approve once')`))throw Error('Resolved permission remains actionable');
  await nav('permissions');await click('Decline');
  if(!(await evaluate(`document.body.innerText.includes('Oliver Reed declined · no draft created.')`)))throw Error('Declined action does not preserve no-change status');
  await nav('slideout');await input('[aria-label="Message Billing Agent"]','Private design message');await click('Send message');
  if(!(await evaluate(`document.querySelector('.cc-drawer-messages').textContent.includes('Private design message')`)))throw Error('Private message did not append');
  await aria('Open a team room');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Create shared room').disabled`)))throw Error('Private context can be shared without audience confirmation');
  await click('Cancel');await aria('Open a team room');await aria('Confirm shared room audience');await click('Create shared room');
  await wait(`!!document.querySelector('.cc-room-layout')`,'Shared room did not open');
  if(await evaluate(`document.querySelector('.cc-room-messages').textContent.includes('Private design message')`))throw Error('Private messages leaked into shared room');
  await aria('Invoices');
  if(!(await evaluate(`document.querySelector('.cc-drawer-messages').textContent.includes('Private design message')`)))throw Error('Opening the shared room destroyed the private conversation');
  await aria('Close chat panel');
  if(await evaluate(`!!document.querySelector('.cc-drawer')`))throw Error('Slide-out did not close');
  await click('Ask about this invoice');
  await nav('collaboration');
  if(!(await evaluate(`[...document.querySelectorAll('.chat-design img[data-agent-portrait]')].every(img=>img.complete && img.naturalWidth>0) && new Set([...document.querySelectorAll('[data-agent-portrait]')].map(img=>img.dataset.agentPortrait)).size===3`)))throw Error('Agent headshots did not load in the collaborative room');
  const authors=await evaluate(`[...new Set([...document.querySelectorAll('[data-author]')].map(e=>e.dataset.author))]`);
  if(!['oliver','maria','billing','ledger','compliance'].every(id=>authors.includes(id)))throw Error('Multiple named human and agent contributions are missing');
  await aria('View Ledger Agent permissions');
  if(!(await evaluate(`document.querySelector('.cc-member-access').textContent.includes('Cannot approve financial actions')`)))throw Error('Agent approval boundary is unclear');
  await click('Review proposal');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Approve proposal').disabled`)))throw Error('Operations participant can approve a financial proposal');
  await click('Close');
  await evaluate(`document.querySelector('[aria-label="Room participants and context"] button').click();true`);
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Invite teammate').disabled`)))throw Error('Invite bypasses participant choice');
  await aria('Select Sam Chen');await click('Invite teammate');
  if(!(await evaluate(`document.querySelector('.cc-participant-count').textContent.includes('3 people · 3 agents')`)))throw Error('Human participant list did not update');
  await input('[aria-label="Message the shared room"]','@Billing Agent please prepare the review summary.');await click('Send message');
  if(!(await evaluate(`document.querySelector('.cc-room-messages').textContent.includes('please prepare the review summary')`)))throw Error('Room message did not append');
  await input('[aria-label="Search conversations"]','Northstar');
  if(await evaluate(`document.querySelectorAll('.cc-thread').length`)!==1)throw Error('Conversation search did not filter');
  await evaluate(`document.querySelector('.cc-thread').click();true`);
  if(await evaluate(`document.querySelector('.cc-room-messages').textContent.includes('please prepare the review summary')`))throw Error('Shared message leaked into private conversation');
  await nav('proposal-review');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Approve proposal').disabled`)))throw Error('Finance approval skips exact-action confirmation');
  await aria('Confirm exact payment proposal');await click('Approve proposal');
  if(!(await evaluate(`document.querySelector('.cc-proposal-card').textContent.includes('Approved · queued') && document.body.innerText.includes('no capture or ledger posting is confirmed')`)))throw Error('Approval was presented as financial execution');
  await nav('proposal-review');await click('Reject proposal');
  if(!(await evaluate(`document.querySelector('.cc-proposal-card').textContent.includes('Rejected')`)))throw Error('Proposal rejection did not resolve');
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  await page('Page.navigate',{url:origin+'/chat.html'});
  await wait(`document.querySelectorAll('[data-dc-slot]').length===6`,'Editable chat artboards did not register');
  for(const scene of scenes){
    await evaluate(`document.querySelector('[data-dc-slot="ai-chat-${scene==='collaboration'?'fullpage':scene}"] .dc-labeltext').click();true`);
    await wait(`document.querySelectorAll('.chat-design').length===7`,'Chat artboard focus did not open');
    for(const theme of ['light','dark']){await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,150))`);await shot(`chat-artboard-${scene}-${theme}@2x.png`);}
    await evaluate(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));true`);
  }
  await page('Page.navigate',{url:origin+'/index.html'});
  await wait(`document.querySelectorAll('[data-dc-section="ai-chat"] [data-dc-slot]').length===6`,'Full canvas chat registration is missing');
  await evaluate(`document.querySelector('[data-dc-slot="ai-chat-fullpage"] .dc-labeltext').click();true`);
  await wait(`document.querySelectorAll('.chat-design').length===7`,'Main canvas chat focus failed');
  if(apiCalls)throw Error('Chat designs called application APIs');
  if(errors.length)throw Error(errors.join('\n'));
  await writeFile(path.join(output,'chat-design-report.json'),JSON.stringify({checks:['6 editable chat artboards','12 clean light/dark marketing captures','12 editable-artboard previews','shared SVGs, CGI agent portraits and semantic tokens','contextual response with ledger references','one-time permission approval and decline','explicit room audience confirmation','private and shared message isolation','two human and three agent authors','participant-specific permission inspector','Operations role cannot approve money movement','finance approval requires exact-action confirmation','approval queues execution without claiming capture or ledger posting','human invite and local message composition','conversation search','focused and full canvas registration','human mention and agent message notifications','notification filters, read, dismiss and linked-message focus','no application API calls','no runtime exceptions'],scenes},null,2)+'\n');
  console.log('Chat design checks passed: 6 artboards, 24 captures, no application API calls.');
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
