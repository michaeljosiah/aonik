// Browser verification of real shared UI components with fictional props. No application API calls.
// Start Vite with VITE_AUTH_PROVIDER=mock on port 4175 before running this script.
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { readFile, writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
const app = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.resolve(app,'../../Templates/aonik-admin-starterkit/marketing/screenshots');
const origin = process.env.AGENT_TEST_ORIGIN || 'http://127.0.0.1:4175';
const cache = path.join(process.env.LOCALAPPDATA || '', 'ms-playwright');
const versions = (await readdir(cache)).filter(n => /^chromium-\d+$/.test(n)).sort((a,b) => Number(b.split('-')[1])-Number(a.split('-')[1]));
const browserPath = process.env.CHROME_PATH || versions.map(v => path.join(cache,v,'chrome-win64/chrome.exe')).find(existsSync);
if (!browserPath) throw new Error('Set CHROME_PATH to a Chromium browser executable.');
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
    const timer=setTimeout(() => {pending.delete(id);reject(new Error(`CDP timeout: ${method}`));},60000);
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

  const previewUrl=origin+'/__agent-identity-preview';
  let apiCalls=0;
  await page('Fetch.enable',{patterns:[{urlPattern:previewUrl},{urlPattern:'*/api/*'}]});
  socket.addEventListener('message',async event=>{
    const data=JSON.parse(event.data);
    if(data.method!=='Fetch.requestPaused')return;
    const {requestId,request}=data.params;
    if(request.url!==previewUrl)apiCalls++;
    const content=request.url===previewUrl?'<!doctype html><html><head><title>Agent identity component verification</title></head><body><div id="preview"></div></body></html>':'{}';
    await page('Fetch.fulfillRequest',{requestId,responseCode:200,responseHeaders:[{name:'Content-Type',value:request.url===previewUrl?'text/html':'application/json'}],body:Buffer.from(content).toString('base64')});
  });
  await page('Page.navigate',{url:previewUrl});
  const wait=async(expression,label)=>{const start=Date.now();while(!(await evaluate(expression))){if(Date.now()-start>20000)throw Error(label+': '+errors.join('\n'));await new Promise(resolve=>setTimeout(resolve,100));}};
  await wait(`!!document.querySelector('#preview')`,'Preview host');
  await evaluate(`(async()=>{
    const refresh=(await import('/@react-refresh')).default;
    refresh.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;
    await import('/src/index.css');
    const React=(await import('/node_modules/.vite/deps/react.js')).default;
    const client=await import('/node_modules/.vite/deps/react-dom_client.js');
    const createRoot=client.createRoot ?? client.default.createRoot;
    const panelSource=await fetch('/src/components/layout/NotificationsPanel.tsx').then(response=>response.text());
    const routerUrl=panelSource.match(/from ["']([^"']*react-router-dom[^"']*)["']/)[1];
    const {MemoryRouter}=await import(routerUrl);
    const {ChatMessageList}=await import('/src/components/ai/ChatMessageList.tsx');
    const {AonikCommentary}=await import('/src/components/ai/AonikCommentary.tsx');
    const {NotificationsPanel}=await import('/src/components/layout/NotificationsPanel.tsx');
    const h=React.createElement;window.avatarActions=[];
    const notifications=[['finance','Agent','finance-agent','Finance review ready','Warning'],['aonik','Agent','master-orchestrator','Workspace brief ready','Info'],['unknown','Agent',null,'Unattributed agent message','Info'],['system','System','finance-agent','System maintenance complete','Success']].map(([id,source,agentName,title,severity])=>({id,source,title,severity,metadataJson:JSON.stringify({agentName}),body:'Fictional notification for avatar verification.',status:'Unread',createdAt:new Date().toISOString(),actionUrl:null}));
    createRoot(document.querySelector('#preview')).render(h(MemoryRouter,{},h('main',{className:'p-8 max-w-4xl'},h('h1',{className:'text-2xl font-semibold'},'Workspace'),h(AonikCommentary,{},'3 proposals waiting · 4 invoices unpaid · cash position updated just now'),h('section',{className:'mt-8'},h(ChatMessageList,{messages:[{type:'assistant',id:'a',agentName:'master-orchestrator',content:'Here is your workspace brief.'},{type:'assistant',id:'f',agentName:'finance-agent',content:'The invoice references are ready for review.'},{type:'assistant',id:'p',agentName:'platform-agent',content:'Workspace access checks are complete.'},{type:'assistant',id:'legacy',content:'A legacy message without an author.'}],isStreaming:false})),h(NotificationsPanel,{open:true,onClose:()=>{},notifications,unreadCount:4,loading:false,onMarkRead:async id=>window.avatarActions.push(['read',id]),onDismiss:async id=>window.avatarActions.push(['dismiss',id]),onMarkAllRead:async()=>window.avatarActions.push(['all'])}))));
    return true;
  })()`);
  await wait(`document.images.length===6 && [...document.images].every(img=>img.complete && img.naturalWidth>0)`,'Six identified portraits did not decode');
  if(!(await evaluate(`document.body.innerText.includes('AI assistant') && document.body.innerText.includes('Finance Agent · AI agent') && document.body.innerText.includes('Warning') && document.body.innerText.includes('System maintenance complete')`)))throw Error('Agent labels, severity or system identity missing');
  if(await evaluate(`document.querySelectorAll('article img').length`)!==2)throw Error('Only two explicitly identified agent notifications should have portraits');
  await evaluate(`[...document.querySelectorAll('article')].find(el=>el.textContent.includes('Finance review ready')).querySelectorAll('button')[0].click();true`);
  await wait(`window.avatarActions.some(([action,id])=>action==='read' && id==='finance')`,'Mark read stopped working');
  await mkdir(output,{recursive:true});
  for(const theme of ['light','dark']){
    await evaluate(`document.documentElement.classList.toggle('dark',${theme==='dark'});new Promise(resolve=>setTimeout(resolve,150))`);
    const shot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,`agent-surfaces-runtime-${theme}@2x.png`),Buffer.from(shot.data,'base64'));
  }
  if(apiCalls)throw Error('Component fixtures called application APIs');
  if(errors.length)throw Error(errors.join('\n'));
  await writeFile(path.join(output,'agent-identity-runtime-report.json'),JSON.stringify({checks:['AONIK workspace commentary','AONIK and domain chat authors','generic legacy author fallback','two explicit notification authors','system metadata cannot assign an agent portrait','notification severity retained','mark-read callback retained','six decoded portraits','light/dark shared component captures','zero API calls'],apiCalls},null,2)+'\n');
  console.log('Runtime agent identity checks passed: chat, commentary, notification authors, fallbacks and mark-read action.');
} finally {
  socket?.close();chrome.kill();
  await new Promise(resolve=>chrome.exitCode!==null?resolve():chrome.once('exit',resolve));
  const resolved=path.resolve(userData);
  if(path.dirname(resolved)===tempRoot && path.basename(resolved).startsWith('aonik-showcase-')) await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
}
