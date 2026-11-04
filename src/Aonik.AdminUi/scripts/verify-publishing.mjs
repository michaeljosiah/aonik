// Separate Arke verifier. Real UI with intercepted test responses; no backend calls or writes.
// Start Vite with VITE_AUTH_PROVIDER=mock on port 4176.
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import vm from 'node:vm';
import { readFile, writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
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

  const kit=path.resolve(app,'../../Templates/aonik-admin-starterkit');const context=vm.createContext({structuredClone});
  vm.runInContext(await readFile(path.join(kit,'screens/arke-publishing-data.js'),'utf8'),context);
  const fixtures=vm.runInContext('({review:AP_REVIEW,release:AP_RELEASE,channel:AP_CHANNEL,operation:AP_OPERATION,edition:AP_EDITION,ids:AP_IDS})',context);
  let review=structuredClone(fixtures.review),release={...structuredClone(fixtures.release),status:'in-review',decisionId:null,blockingCode:'active-approval-required'},readOnly=false,failNext=null;
  const tenant='11111111-1111-4111-8111-111111111111',requests=[];
  await page('Page.addScriptToEvaluateOnNewDocument',{source:`localStorage.setItem('selected_tenant_id','${tenant}');localStorage.setItem('selected_tenant',JSON.stringify({tenantId:'${tenant}',name:'Arke Worlds',environment:'Test'}));localStorage.setItem('aonik-theme','light');`});
  await page('Fetch.enable',{patterns:[{urlPattern:'*/api/*'}]});
  socket.addEventListener('message',async event=>{
    const message=JSON.parse(event.data);if(message.method!=='Fetch.requestPaused')return;
    const {requestId,request}=message.params,url=new URL(request.url),route=url.pathname.replace(/^\/api/,'');requests.push({route,method:request.method,body:request.postData?JSON.parse(request.postData):undefined});let data={items:[],totalCount:0},code=200,type='application/json',raw=null;
    if(route==='/admin/manifest')data={businessType:'arke-worlds',enabledModules:['platform','publishing','ordering','finance','subscriptions','groups','workspaces','documents','ai','agents'],modules:[],featureFlags:{},permissions:['Publishing.Review','Publishing.Publish','Publishing.Withdraw','Invoice.Read','Customers.Read'],allowedPolicies:['AdminUserPolicy','AdminPolicy','AdminReadPolicy','PublishingReviewPolicy','PublishingManagementPolicy','PublishingPublishPolicy','PublishingWithdrawPolicy']};
    else if(route==='/host/me/tenants')data={tenants:[{tenantId:tenant,name:'Arke Worlds',environment:'Test',roles:['Operations'],isActive:true}]};
    else if(route==='/publishing/management/channels')data=[{...fixtures.channel,canPublish:!readOnly,canWithdraw:!readOnly}];
    else if(route==='/publishing/review/submissions')data={page:1,pageSize:25,totalCount:1,items:[review.submission]};
    else if(route===`/publishing/review/submissions/${fixtures.ids.receipt}`)data=review;
    else if(route.endsWith('/images/key-art')){type='image/png';raw=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=','base64');}
    else if(route.endsWith('/decisions')&&request.method==='POST'){
      if(failNext==='stale'){failNext=null;code=409;data={code:'review-version-changed',detail:'Review changed. Reload the current revision.'};}
      else {const command=JSON.parse(request.postData);review.submission.status='approved';review.submission.version='server-approved-v3';review.blockingCode='submission-not-in-review';review.decisions.unshift({id:fixtures.ids.decision,...command,actorUserId:'independent-editor',actorPartyId:'editorial-team',policyVersion:'review-v1',decidedAt:'2026-11-04T10:20:00Z',isActive:true});release={...fixtures.release,version:review.submission.version};data=review.decisions[0];}
    }
    else if(route.endsWith('/publication')&&request.method==='GET')data=release;
    else if(route.endsWith('/publication')&&request.method==='POST'){
      if(failNext==='uncertain'){failNext=null;code=503;data={};}
      else{data={...fixtures.operation,status:'pending',code:'queued',canRetry:false};release={...release,status:'publishing',operations:[data]};review.submission.status='publishing';}
    }
    else if(route==='/publishing/management/channels/arke-worlds/catalogue')data={channel:{...fixtures.channel,canPublish:!readOnly,canWithdraw:!readOnly},page:1,pageSize:25,totalEditions:1,editions:[fixtures.edition],operations:release.operations,approved:[]};
    else if(route.includes('unread')||route.includes('summary'))data={count:0,unreadCount:0};
    else if(route.includes('notifications')||route.includes('agents'))data=[];
    try{await page('Fetch.fulfillRequest',{requestId,responseCode:code,responseHeaders:[{name:'Content-Type',value:type}],body:(raw||Buffer.from(JSON.stringify(data))).toString('base64')});}catch{/* Aborted navigation. */}
  });
  const wait=async(expression,label)=>{const start=Date.now();while(!(await evaluate(expression))){if(Date.now()-start>30000)throw Error(label+': '+await evaluate('document.body.innerText.slice(0,1600)')+' '+errors.join(' | '));await new Promise(r=>setTimeout(r,100));}};
  const click=label=>evaluate(`(()=>{const scope=document.querySelector('dialog[open]')||document.querySelector('[data-publishing-runtime]');const b=[...scope.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(label)}||b.getAttribute('aria-label')===${JSON.stringify(label)});if(!b)throw Error('Missing '+${JSON.stringify(label)});b.click();})()`);
  const text=(label,value)=>evaluate(`(()=>{const e=document.querySelector('[aria-label='+${JSON.stringify(JSON.stringify(label))}+']');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  const reviewRoute=origin+'/arke/submissions/'+fixtures.ids.receipt;
  await page('Page.navigate',{url:origin+'/arke/submissions'});await wait(`document.querySelector('.ap-record-link')?.textContent.includes('Lantern')`,'Review queue');
  await evaluate(`document.querySelector('.ap-record-link').click()`);await wait(`document.querySelector('.ap-tabs[aria-label="Review sections"]')||document.body.innerText.includes('Edition overview')`,'Review detail');
  await mkdir(output,{recursive:true});
  for(const theme of ['light','dark']){await evaluate(`document.documentElement.setAttribute('data-theme','${theme}');true`);const shot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,'publishing-runtime-review-'+theme+'@2x.png'),Buffer.from(shot.data,'base64'));}
  await click('Approve revision');await wait(`!!document.querySelector('dialog[open]')`,'Native confirmation');
  if(!(await evaluate(`document.activeElement?.getAttribute('aria-label')==='Internal assessment'`)))throw Error('Dialog did not focus its assessment field');
  await text('Internal assessment','Rights, audience and attribution checked against the exact package.');await evaluate(`document.querySelectorAll('dialog input[type=checkbox]').forEach(e=>e.click())`);
  await click('Approve revision');await wait(`!document.querySelector('dialog[open]')&&document.body.innerText.includes('Revision approved')`,'Approval outcome');
  const decision=requests.find(r=>r.route.endsWith('/decisions')&&r.method==='POST');if(!decision||decision.body.revisionId!==fixtures.ids.revision||decision.body.reviewDigest!==fixtures.review.snapshot.digest||!Object.values(decision.body.checks).every(Boolean))throw Error('Decision did not bind exact evidence');
  await click('Publication');await wait(`document.body.innerText.includes('Publish approved edition')`,'Publication readiness');await click('Publish approved edition');await wait(`!!document.querySelector('dialog[open]')`,'Release confirmation');
  await text('Action reason','Publish the approved edition for the qualified channel.');await evaluate(`document.querySelector('dialog input[type=checkbox]').click()`);failNext='uncertain';await click('Queue publication');
  await wait(`document.body.innerText.includes('Retry same request')`,'Uncertain publication');
  if(!(await evaluate(`document.querySelector('[aria-label="Action reason"]').disabled`)))throw Error('Uncertain request arguments can change');
  await click('Retry same request');await wait(`!document.querySelector('dialog[open]')&&document.body.innerText.includes('Publication queued')`,'Reconciled release');
  const posts=requests.filter(r=>r.route.endsWith('/publication')&&r.method==='POST');if(posts.length!==2||JSON.stringify(posts[0].body)!==JSON.stringify(posts[1].body))throw Error('Uncertain retry changed operation ID or arguments');
  if(await evaluate(`document.body.innerText.includes('Publication confirmed at serial')`))throw Error('Queue admission misrepresented as public success');
  await page('Page.navigate',{url:origin+'/arke/catalogue'});await wait(`document.body.innerText.includes('Player editions')`,'Catalogue route');
  if(!(await evaluate(`document.body.innerText.includes('53')&&document.body.innerText.includes('Immutable pin')`)))throw Error('Catalogue provenance missing');
  review=structuredClone(fixtures.review);release={...fixtures.release,status:'in-review',decisionId:null,blockingCode:'active-approval-required'};failNext='stale';
  await page('Page.navigate',{url:reviewRoute});await wait(`document.body.innerText.includes('Edition overview')`,'Review reset');await click('Approve revision');await text('Internal assessment','Check exact snapshot.');await evaluate(`document.querySelectorAll('dialog input[type=checkbox]').forEach(e=>e.click())`);await click('Approve revision');await wait(`document.body.innerText.includes('Review changed')`,'Stale conflict');
  if(!(await evaluate(`document.querySelector('.ap-dialog-foot button:last-child').disabled`)))throw Error('Stale confirmation remains enabled');await click('Cancel');
  readOnly=true;await page('Page.navigate',{url:origin+'/arke/catalogue'});await wait(`document.body.innerText.includes('Player editions')`,'Read-only catalogue');if(await evaluate(`[...document.querySelectorAll('[data-publishing-runtime] button')].some(b=>b.textContent==='Withdraw')`))throw Error('Read-only user sees withdrawal');
  const ax=await page('Accessibility.getFullAXTree');const buttons=ax.nodes.filter(n=>n.role?.value==='button'&&!n.ignored);if(buttons.some(n=>!n.name?.value))throw Error('Unnamed accessible button');
  if(errors.length)throw Error(errors.join(' | '));
  await writeFile(path.join(output,'publishing-runtime-report.json'),JSON.stringify({checks:['live routed Admin UI with intercepted API responses','exact revision/digest confirmation','native dialog focus','light and dark review','stable UUID and identical retry body after unknown outcome','queued is not published','409 requires reload','read-only action gates','canonical provenance visible','accessible buttons named','no runtime exceptions'],publishingRequests:requests.filter(r=>r.route.startsWith('/publishing'))},null,2)+'\n');
  console.log('Publishing runtime browser checks passed with intercepted API responses.');
}finally{
  socket?.close();chrome.kill();await new Promise(resolve=>chrome.exitCode!==null?resolve():chrome.once('exit',resolve));
  const resolved=path.resolve(userData);
  if(path.dirname(resolved)===tempRoot&&path.basename(resolved).startsWith('aonik-arke-'))await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
}
