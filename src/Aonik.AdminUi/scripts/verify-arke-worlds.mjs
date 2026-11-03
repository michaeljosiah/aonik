// Separate Arke verifier. Real UI with intercepted test responses; no backend calls or writes.
// Start Vite with VITE_AUTH_PROVIDER=mock on port 4176.
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
const app=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const output=path.resolve(app,'../../Templates/aonik-admin-starterkit/marketing/screenshots');
const origin=process.env.ARKE_TEST_ORIGIN||'http://127.0.0.1:4176';
const cache=path.join(process.env.LOCALAPPDATA||'','ms-playwright');
const versions=(await readdir(cache)).filter(n=>/^chromium-\d+$/.test(n)).sort((a,b)=>Number(b.split('-')[1])-Number(a.split('-')[1]));
const browserPath=process.env.CHROME_PATH||versions.map(v=>path.join(cache,v,'chrome-win64/chrome.exe')).find(existsSync);
if(!browserPath)throw Error('Set CHROME_PATH to Chromium.');
const tempRoot=path.resolve(os.tmpdir()),userData=await mkdtemp(path.join(tempRoot,'aonik-arke-'));
const chrome=spawn(browserPath,['--headless=new','--no-first-run','--no-default-browser-check','--disable-background-networking','--remote-debugging-port=0',`--user-data-dir=${userData}`,'about:blank'],{windowsHide:true,stdio:['ignore','ignore','pipe']});
let socket;
try{
  const wsUrl=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('Chromium launch timed out')),20000);let buffer='';chrome.stderr.on('data',data=>{buffer+=data;const match=buffer.match(/DevTools listening on (ws:\/\/[^\s]+)/);if(match){clearTimeout(timer);resolve(match[1]);}});chrome.on('error',reject);});
  socket=new WebSocket(wsUrl);await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  let sequence=0;const pending=new Map(),errors=[];
  socket.addEventListener('message',event=>{const data=JSON.parse(event.data);if(data.method==='Runtime.exceptionThrown')errors.push(data.params.exceptionDetails.exception?.description||data.params.exceptionDetails.text);if(data.id&&pending.has(data.id)){const {resolve,reject,timer}=pending.get(data.id);clearTimeout(timer);pending.delete(data.id);data.error?reject(Error(JSON.stringify(data.error))):resolve(data.result);}});
  const call=(method,params={},sessionId)=>new Promise((resolve,reject)=>{const id=++sequence,timer=setTimeout(()=>{pending.delete(id);reject(Error('CDP timeout: '+method));},30000);pending.set(id,{resolve,reject,timer});socket.send(JSON.stringify({id,method,params,...(sessionId?{sessionId}:{})}));});
  const {targetId}=await call('Target.createTarget',{url:'about:blank'}),{sessionId}=await call('Target.attachToTarget',{targetId,flatten:true});
  const page=(method,params)=>call(method,params,sessionId);
  const evaluate=async expression=>{const result=await page('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(result.exceptionDetails)throw Error(result.exceptionDetails.exception?.description||result.exceptionDetails.text);return result.result.value;};
  await page('Page.enable');await page('Runtime.enable');await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  const tenant='11111111-1111-4111-8111-111111111111',requests=[];
  await page('Page.addScriptToEvaluateOnNewDocument',{source:`localStorage.setItem('selected_tenant_id','${tenant}');localStorage.setItem('selected_tenant',JSON.stringify({tenantId:'${tenant}',name:'Arke Worlds',environment:'Test'}));localStorage.setItem('aonik-theme','light');`});
  await page('Fetch.enable',{patterns:[{urlPattern:'*/api/*'}]});
  socket.addEventListener('message',async event=>{
    const message=JSON.parse(event.data);if(message.method!=='Fetch.requestPaused')return;
    const {requestId,request}=message.params,route=new URL(request.url).pathname.replace(/^\/api/,'');requests.push({route,method:request.method});let data={items:[],totalCount:0};
    if(route==='/admin/manifest')data={businessType:'arke-worlds',enabledModules:['platform','ordering','finance','subscriptions','groups','workspaces','documents','ai','agents'],modules:[],featureFlags:{},permissions:['Invoice.Read','Ledger.Read','Customers.Read'],allowedPolicies:['AdminUserPolicy','AdminPolicy','AdminReadPolicy','AdminUserWritePolicy']};
    else if(route==='/host/me/tenants')data={tenants:[{tenantId:tenant,name:'Arke Worlds',environment:'Test',roles:['Operations'],isActive:true}]};
    else if(route.includes('unread')||route.includes('summary'))data={count:0,unreadCount:0};
    else if(route.includes('notifications')||route.includes('agents'))data=[];
    try{await page('Fetch.fulfillRequest',{requestId,responseCode:200,responseHeaders:[{name:'Content-Type',value:'application/json'}],body:Buffer.from(JSON.stringify(data)).toString('base64')});}catch{/* Navigation can cancel an intercepted request. */}
  });
  const wait=async(expression,label)=>{const start=Date.now();while(!(await evaluate(expression))){if(Date.now()-start>30000)throw Error(label+': '+await evaluate('document.body.innerText.slice(0,1600)')+' '+errors.join(' | '));await new Promise(r=>setTimeout(r,100));}};
  await page('Page.navigate',{url:origin+'/arke/overview'});await wait(`document.body.innerText.includes('One workspace for the Arke portfolio')`,'Arke workspace did not mount');
  if(!(await evaluate(`document.body.innerText.includes('staff data connections remain to be implemented')&&!!document.querySelector('a[href="/customers"]')&&!!document.querySelector('a[href="/arke/families"]')`)))throw Error('Connection state or shared tools missing');
  await mkdir(output,{recursive:true});
  for(const theme of ['light','dark']){await evaluate(`localStorage.setItem('aonik-theme','${theme}');document.documentElement.setAttribute('data-theme','${theme}');true`);await new Promise(r=>setTimeout(r,250));const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,'arke-worlds-runtime-overview-'+theme+'@2x.png'),Buffer.from(result.data,'base64'));}
  await evaluate(`document.querySelector('a[href="/arke/families"]').click();true`);await wait(`document.body.innerText.includes('Families is awaiting its staff connection')`,'Family navigation failed');
  if(!(await evaluate(`!document.body.innerText.includes('Morgan family')&&document.body.innerText.includes('No records have been loaded')`)))throw Error('Production route contains simulated customer data');
  if(requests.some(r=>r.method!=='GET'))throw Error('Read-only page performed a write');if(errors.length)throw Error(errors.join(' | '));
  await writeFile(path.join(output,'arke-worlds-runtime-report.json'),JSON.stringify({checks:['real Admin UI mounts','Arke profile and shared tools visible','connection states explicit','no simulated production customer data','family navigation works','no writes or runtime exceptions'],requests},null,2)+'\n');
  console.log('Arke Worlds real Admin UI browser checks passed; intercepted API responses only.');
}finally{
  socket?.close();chrome.kill();await new Promise(resolve=>chrome.exitCode!==null?resolve():chrome.once('exit',resolve));
  const resolved=path.resolve(userData);
  if(path.dirname(resolved)===tempRoot&&path.basename(resolved).startsWith('aonik-arke-'))await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
}
