// Runs the real collaborative workspace against fictional API responses in two browser tabs. No live services.
// Start Vite with VITE_AUTH_PROVIDER=mock on port 4175 before running this script.
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { readFile, writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
const app = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.resolve(app,'../../Templates/aonik-admin-starterkit/marketing/screenshots');
const origin = process.env.ROOM_TEST_ORIGIN || 'http://127.0.0.1:4175';
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

  const tenant='11111111-1111-4111-8111-111111111111';
  const owner='22222222-2222-4222-8222-222222222222', maria='33333333-3333-4333-8333-333333333333', sam='44444444-4444-4444-8444-444444444444';
  const roomId='55555555-5555-4555-8555-555555555555';
  const agentNames=['master-orchestrator','finance-agent','platform-agent','commerce-agent'];
  const agentOptions=agentNames.map(name=>({name,description:name,scope:name==='master-orchestrator'?'Shared room context only; no private memory or tools':'Scoped domain tools under the requesting human’s permissions'}));
  const participants=[{actorKey:'human:'+owner,userId:owner,displayName:'Oliver Reed',role:'Owner',lastSeenAt:new Date().toISOString()},{actorKey:'human:'+maria,userId:maria,displayName:'Maria Gomez',role:'Participant',lastSeenAt:new Date().toISOString()},...agentNames.map(name=>({actorKey:'agent:'+name,agentName:name,displayName:name==='master-orchestrator'?'AONIK':name,role:'Agent'}))];
  let sequenceNumber=200;
  const message=(kind,author,content,agentName=null)=>({id:crypto.randomUUID(),sequence:++sequenceNumber,actorKind:kind,authorUserId:kind==='Human'?author:null,agentName,authorName:agentName || (author===owner?'Oliver Reed':'Maria Gomez'),content,mentionKeys:[],aiRunId:kind==='Agent'?crypto.randomUUID():null,createdAt:new Date().toISOString()});
  const messages=[message('Human',owner,'Let’s review the November collections position together. We have linked invoice INV-2048 for context.'),message('Human',maria,'I will review any proposed changes. Please distinguish the customer balance from payment execution.'),message('Agent',null,'I can summarise this room’s discussion. Address Finance Agent for live financial evidence.','master-orchestrator'),message('Agent',null,'The linked invoice reference is ready for a domain review. No payment has been executed.','finance-agent'),message('Agent',null,'Workspace permissions continue to apply to every participant and proposed action.','platform-agent')];
  const archived={...message('Human',maria,'Please review the earlier customer reference before proposing a draft.'),sequence:50,mentionKeys:['human:'+owner]};sequenceNumber--;
  const notes=new Map([[owner,[]],[maria,[]]]);
  const addNote=(user,m,kind='message',read=false)=>{const n={id:crypto.randomUUID(),tenantId:tenant,userId:user,channel:'InApp',type:'chat.'+kind,source:m.agentName?'Agent':'Chat',title:kind==='mention'?'You were mentioned in a shared room':'New message in a shared room',body:'Open the room to read the message. Access follows current room membership.',severity:'Info',status:read?'Read':'Unread',actionUrl:`/ai/chat?view=rooms&room=${roomId}&message=${m.id}&sequence=${m.sequence}`,correlationId:'chat:'+roomId+':'+m.id,aiRunId:m.aiRunId,metadataJson:JSON.stringify({roomId,roomTitle:'November collections review',messageId:m.id,sequence:m.sequence,actorKind:m.actorKind,authorName:m.agentName==='master-orchestrator'?'AONIK':m.authorName,agentName:m.agentName}),createdAt:new Date().toISOString(),readAt:read?new Date().toISOString():null,dismissedAt:null};notes.get(user).unshift(n);return n;};
  addNote(owner,messages[1],'message',true);addNote(owner,messages[3]);addNote(owner,messages[2]);
  const mentionNote=addNote(owner,archived,'mention');
  let failNextRead=false;
  let roleOverride=null,removed=false,consumed=false;
  const turns=[],requests=[],sent=[],decisions=[],invites=[];
  const summary=user=>({id:roomId,title:'November collections review',role:user===owner?'Owner':roleOverride || 'Participant',revision:messages.length,lastActivityAt:new Date().toISOString(),humanCount:participants.filter(p=>p.userId).length,agentCount:participants.filter(p=>p.agentName).length});
  const detail=user=>({room:summary(user),participants:participants.map(p=>p.userId===maria?{...p,role:roleOverride || p.role}:p),messages,turns:turns.map(t=>({...t,canClose:false,approvals:t.approvals.map(a=>({...a,canDecide:a.status==='Pending' && t.requestedByUserId===user && roleOverride!=='Observer',canContinue:a.status==='Approved' && !consumed && t.requestedByUserId===user}))})),context:[{type:'Invoice',reference:'INV-2048'},{type:'Order',reference:'ORD-2048'}],hasEarlier:false,currentUserId:user});
  const actors=new Map([[sessionId,owner]]);
  const intercept=async event=>{
    const data=JSON.parse(event.data);if(data.method!=='Fetch.requestPaused')return;
    const sid=data.sessionId,user=actors.get(sid) || owner;
    const {requestId,request}=data.params;const url=new URL(request.url),route=url.pathname.replace(/^\/api/,'');
    requests.push({route,method:request.method});let status=200,payload={items:[],totalCount:0,pageNumber:1,pageSize:20};
    const body=request.postData?JSON.parse(request.postData):{};
    if(route==='/host/me/tenants')payload={tenants:[{tenantId:tenant,name:'Primrose Logistics',environment:'Test',roles:['Operations'],isActive:true}]};
    else if(route==='/admin/manifest')payload={businessType:'remittance',enabledModules:['platform','ordering','finance','ai','agents','documents'],modules:[],permissions:['Ai.Read','Ai.Write','Payment.Read','Payment.Create','Customers.Read','Ledger.Read'],allowedPolicies:['AdminUserPolicy','AdminReadPolicy','AdminWritePolicy','AdminPolicy','AdminUserWritePolicy'],featureFlags:{}};
    else if(route==='/admin/notifications')payload=notes.get(user).filter(n=>n.status!=='Dismissed');
    else if(route==='/admin/notifications/summary')payload={unreadCount:notes.get(user).filter(n=>n.status==='Unread').length};
    else if(route==='/admin/notifications/read-all'){const unread=notes.get(user).filter(n=>n.status==='Unread');unread.forEach(n=>{n.status='Read';n.readAt=new Date().toISOString();});payload={affectedCount:unread.length};}
    else if(route.startsWith('/admin/notifications/') && request.method==='POST'){
      const n=notes.get(user).find(n=>route.includes(n.id));
      if(failNextRead){failNextRead=false;status=503;payload={message:'Fictional temporary failure'};}
      else {n.status=route.endsWith('/dismiss')?'Dismissed':'Read';n.readAt=new Date().toISOString();payload=n;}
    }
    else if(route==='/admin/notifications/stream')payload={};
    else if(route==='/ai/threads')payload={threads:[{id:'private-thread',title:'Private payroll note',status:'Active',messageCount:2,createdAt:new Date().toISOString()}]};
    else if(route==='/ai/rooms/agents')payload=agentOptions;
    else if(route==='/ai/rooms/people')payload=[{userId:sam,displayName:'Sam Chen'}];
    else if(route==='/ai/rooms' && request.method==='GET')payload=removed && user===maria?[]:[summary(user)];
    else if(route==='/ai/rooms' && request.method==='POST'){requests.at(-1).body=body;payload=detail(user);}
    else if(route===`/ai/rooms/${roomId}`){if(removed && user===maria){status=404;payload={message:'Room not found.'};}else {payload=detail(user);payload.hasEarlier=true;if(url.searchParams.get('before') && Number(url.searchParams.get('before'))<100){payload.messages=[archived];payload.hasEarlier=false;}}}
    else if(route.endsWith('/presence'))payload={success:true};
    else if(route.endsWith('/messages')){
      sent.push(body);const existing=messages.find(m=>m.clientRequestId===body.clientRequestId);
      payload=existing || {...message('Human',user,body.content),mentionKeys:body.mentionKeys,clientRequestId:body.clientRequestId};if(!existing){messages.push(payload);for(const peer of [owner,maria].filter(id=>id!==user))addNote(peer,payload,body.mentionKeys?.includes('human:'+peer)?'mention':'message');}
    }
    else if(route.endsWith('/turns')){
      const turn={id:crypto.randomUUID(),promptMessageId:body.promptMessageId,requestedByUserId:user,agentName:body.agentName,status:'Completed',aiRunId:crypto.randomUUID(),startedAt:new Date().toISOString(),approvals:[],canClose:false};
      if(body.continueApprovalId){consumed=true;messages.push({...message('Agent',null,'The approved action was handed back to the guarded domain tool. Check the invoice record for the authoritative result.',body.agentName),turnId:turn.id});}
      else {turn.approvals=[{id:'66666666-6666-4666-8666-666666666666',tool:'finance_create_invoice',tier:'Medium',actionKind:'Create invoice draft',status:'Pending',argumentsJson:'{"customerId":"CUS-1086","currency":"GBP","amount":4250}',canDecide:true,canContinue:false}];messages.push({...message('Agent',null,'I have prepared a draft-invoice request. The exact proposed arguments need human approval before the tool can run.',body.agentName),turnId:turn.id});}
      turns.push(turn);payload=turn;
    }
    else if(route.endsWith('/approvals/decide')){decisions.push(body);turns.flatMap(t=>t.approvals).find(a=>a.id===body.approvalId).status=body.decision==='Approve'?'Approved':'Rejected';payload={success:true};}
    else if(route.endsWith('/members') && request.method==='POST'){invites.push(body);participants.push({actorKey:body.actorKey,userId:sam,displayName:'Sam Chen',role:body.role});payload={success:true};}
    else if(route.includes('/members/') && request.method==='DELETE'){removed=true;payload={success:true};}
    try{await call('Fetch.fulfillRequest',{requestId,responseCode:status,responseHeaders:[{name:'Content-Type',value:'application/json'}],body:Buffer.from(JSON.stringify(payload)).toString('base64')},sid);}catch{}
  };
  socket.addEventListener('message',event=>void intercept(event));
  const setup=async(sid,user)=>{
    actors.set(sid,user);
    await call('Page.enable',{},sid);await call('Runtime.enable',{},sid);
    await call('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false},sid);
    await call('Page.addScriptToEvaluateOnNewDocument',{source:`localStorage.setItem('selected_tenant_id','${tenant}');localStorage.setItem('selected_tenant',JSON.stringify({tenantId:'${tenant}',name:'Primrose Logistics',environment:'Test'}));localStorage.setItem('aonik-theme','light');`},sid);
    await call('Fetch.enable',{patterns:[{urlPattern:'*/api/*'}]},sid);
    await call('Page.navigate',{url:origin+'/ai/chat?view=rooms&room='+roomId},sid);
  };
  const evalOn=async(sid,expression)=>{const result=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true},sid);if(result.exceptionDetails)throw Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);return result.result.value;};
  const wait=async(sid,expression,label)=>{await call('Page.bringToFront',{},sid);const start=Date.now();while(!(await evalOn(sid,expression))){if(Date.now()-start>30000)throw Error(label+': '+errors.join(' | ')+' '+await evalOn(sid,'document.body.innerText.slice(0,1200)'));await new Promise(resolve=>setTimeout(resolve,100));}};
  const click=async(sid,text)=>{await call('Page.bringToFront',{},sid);return evalOn(sid,`[...document.querySelectorAll('button')].find(el=>el.textContent.trim()===${JSON.stringify(text)}).click();true`);};
  const type=async(sid,text)=>{await call('Page.bringToFront',{},sid);await evalOn(sid,`document.querySelector('textarea[aria-label="Message shared room"]').focus();true`);await call('Input.insertText',{text},sid);};
  await setup(sessionId,owner);
  await wait(sessionId,`!!document.querySelector('[data-component="collaborative-chat"]') && document.body.innerText.includes('November collections review')`,'Owner room did not load');
  const second=await call('Target.createTarget',{url:'about:blank'});const attached=await call('Target.attachToTarget',{targetId:second.targetId,flatten:true});const peer=attached.sessionId;
  await setup(peer,maria);
  await wait(peer,`document.body.innerText.includes('November collections review')`,'Second human room did not load');
  await type(sessionId,'Please check the invoice references before proposing any changes.');await click(sessionId,'Send');
  await wait(peer,`document.body.innerText.includes('Please check the invoice references before proposing any changes.')`,'Owner message did not reach the other human');
  await wait(peer,`!!document.querySelector('button[aria-label="Notifications, 1 unread"]')`,'Worker-style persisted notification did not reconcile across clients');
  await type(peer,'I will verify the customer and review the proposed draft.');await click(peer,'Send');
  await wait(sessionId,`document.body.innerText.includes('I will verify the customer and review the proposed draft.')`,'Second human message did not reach owner');
  await evalOn(sessionId,`document.querySelector('[aria-label="Message recipient"]').click();true`);
  await evalOn(sessionId,`[...document.querySelectorAll('[role="option"]')].find(el=>el.textContent.includes('Ask Finance Agent')).click();true`);
  await type(sessionId,'Prepare a draft invoice for the linked customer.');await click(sessionId,'Send');
  await wait(sessionId,`document.body.innerText.includes('Create invoice draft')`,'Agent response/approval missing');
  await wait(peer,`document.body.innerText.includes('Create invoice draft')`,'Approval did not reach peer');
  if(!(await evalOn(peer,`[...document.querySelectorAll('button')].find(el=>el.textContent.trim()==='Approve').disabled`)))throw Error('Another human can decide the requester-owned approval');
  await click(sessionId,'Approve');await wait(sessionId,`!!document.querySelector('[role="dialog"]')`,'Confirmation missing');await click(sessionId,'Cancel');
  if(decisions.length)throw Error('Opening an approval confirmation executed a decision');
  await click(sessionId,'Approve');await click(sessionId,'Confirm approve');
  await wait(sessionId,`document.body.innerText.includes('Continue this approved action')`,'Approved action continuation missing');
  if(decisions.length!==1)throw Error('Decision did not remain single-use');
  await click(sessionId,'Continue this approved action');
  await wait(sessionId,`document.body.innerText.includes('approved action was handed back')`,'Approved continuation missing');
  await click(sessionId,'Invite');await evalOn(sessionId,`document.querySelector('[aria-label="Choose colleague"]').click();true`);
  await wait(sessionId,`[...document.querySelectorAll('[role="option"]')].some(el=>el.textContent.includes('Sam Chen'))`,'Staff directory missing');
  await evalOn(sessionId,`[...document.querySelectorAll('[role="option"]')].find(el=>el.textContent.includes('Sam Chen')).click();true`);
  if(!(await evalOn(sessionId,`[...document.querySelectorAll('button')].find(el=>el.textContent.trim()==='Add participant').disabled`)))throw Error('History consent is optional');
  await evalOn(sessionId,`document.querySelector('#share-room-history').click();true`);await click(sessionId,'Add participant');
  await wait(peer,`document.body.innerText.includes('Sam Chen')`,'Invite did not update other participant');
  const themedCapture=async prefix=>{
    let lightBackground;
    for(const theme of ['light','dark']){
      const background=await evalOn(sessionId,`(async()=>{document.documentElement.dataset.theme=${JSON.stringify(theme)};await new Promise(resolve=>setTimeout(resolve,600));return getComputedStyle(document.documentElement).getPropertyValue('--background').trim();})()`);
      if(theme==='light')lightBackground=background;
      else if(background===lightBackground)throw Error('Dark capture did not apply the application theme tokens');
      const shot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
      await writeFile(path.join(output,`${prefix}-${theme}@2x.png`),Buffer.from(shot.data,'base64'));
    }
  };
  await mkdir(output,{recursive:true});
  await evalOn(sessionId,`Promise.all([...document.querySelectorAll('[data-component="collaborative-chat"] img')].map(img=>img.decode())).then(()=>true)`);
  if(await evalOn(sessionId,`document.documentElement.scrollWidth>innerWidth`))throw Error('Shared workspace overflows horizontally');
  await themedCapture('chat-collaboration-runtime');
  const openNotifications=async()=>{await call('Page.bringToFront',{},sessionId);await evalOn(sessionId,`document.querySelector('button[aria-label^="Notifications"]').click();true`);await wait(sessionId,`!!document.querySelector('[data-notification-id]')`,'Notification inbox did not open');};
  await openNotifications();
  await evalOn(sessionId,`Promise.all([...document.querySelectorAll('[role="dialog"] img')].map(img=>img.decode())).then(()=>true)`);
  if(!(await evalOn(sessionId,`document.querySelector('[role="dialog"]').textContent.includes('AONIK · AI agent') && document.querySelector('[role="dialog"]').textContent.includes('Maria Gomez · Human')`)))throw Error('Notification author identities missing');
  await themedCapture('chat-notifications-runtime');
  const selectTab=async label=>{await evalOn(sessionId,`(()=>{const tab=[...document.querySelectorAll('[role="tab"]')].find(el=>el.textContent.trim().startsWith(${JSON.stringify(label)}));tab.dispatchEvent(new MouseEvent('mousedown',{bubbles:true,button:0}));tab.click();return true;})()`);await wait(sessionId,`[...document.querySelectorAll('[role="tab"]')].some(el=>el.textContent.trim().startsWith(${JSON.stringify(label)}) && el.dataset.state==='active')`,'Notification filter did not activate');};
  await selectTab('Mentions');
  if(await evalOn(sessionId,`document.querySelectorAll('[data-notification-id]').length`)!==1)throw Error('Mentions filter included ordinary messages');
  await themedCapture('chat-mentions-runtime');
  failNextRead=true;await click(sessionId,'Open message');
  await wait(sessionId,`document.body.innerText.includes('Could not mark this notification read')`,'Read failure feedback missing');
  if(await evalOn(sessionId,`location.search.includes('message=')`))throw Error('Failed read unexpectedly navigated');
  await click(sessionId,'Open message');
  await wait(sessionId,`!!document.querySelector('[data-notification-target="${archived.id}"]')`,'Earlier message link did not load and highlight its page');
  if(!(await evalOn(sessionId,`document.body.innerText.includes('Load messages between these points')`)))throw Error('Earlier notification concealed a message history gap');
  if(mentionNote.status!=='Read')throw Error('Opening did not persist read state');
  await openNotifications();await selectTab('Unread');
  if(await evalOn(sessionId,`!!document.querySelector('[data-notification-id="${mentionNote.id}"]')`))throw Error('Read mention remains in unread filter');
  await click(sessionId,'Dismiss');
  await click(sessionId,'Mark all read');
  await wait(sessionId,`document.querySelector('[role="dialog"]').textContent.includes('You are all caught up')`,'Mark all read did not clear unread state');
  await evalOn(sessionId,`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));true`);
  await page('Emulation.setDeviceMetricsOverride',{width:900,height:1100,deviceScaleFactor:2,mobile:false});
  await click(sessionId,'Participants');
  await wait(sessionId,`!!document.querySelector('[role="dialog"]') && document.querySelector('[role="dialog"]').textContent.includes('Sam Chen')`,'Mobile participant drawer is unavailable');
  roleOverride='Observer';
  await wait(peer,`document.querySelector('textarea[aria-label="Message shared room"]').disabled`,'Observer can still compose');
  removed=true;
  await wait(peer,`!document.querySelector('[data-room-message]')`,'Revoked member still sees room history');
  if(errors.length)throw Error(errors.join('\n'));
  await writeFile(path.join(output,'collaborative-chat-runtime-report.json'),JSON.stringify({checks:['two human browser clients','live shared message updates','scoped agent response and avatar','requester-only approval actions','explicit approval confirmation','guarded medium continuation','history acknowledgement before invitation','participant updates across clients','observer read-only composer','revoked-member history cleared','decoded portraits','light/dark production captures','mobile participant drawer','persisted worker notifications reconcile without SSE','human and agent inbox identity','mention and unread filters','read failure recovery','older message page and focus','read, dismiss and mark-all actions','four production notification captures','light and dark use distinct application theme tokens'],sent:sent.length,decisions:decisions.length,invites:invites.length},null,2)+'\n');
  console.log('Collaborative chat browser checks passed: two humans, agent responses, approvals, invites, observer, revocation, notification polling, filters and linked history states.');
} finally {
  socket?.close();chrome.kill();
  await new Promise(resolve=>chrome.exitCode!==null?resolve():chrome.once('exit',resolve));
  const resolved=path.resolve(userData);
  if(path.dirname(resolved)===tempRoot && path.basename(resolved).startsWith('aonik-showcase-')) await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
}
