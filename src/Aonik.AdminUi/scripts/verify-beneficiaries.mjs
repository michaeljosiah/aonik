// Runs the REAL Admin UI against intercepted, fictional API responses. No backend calls are sent.
// Start Vite with VITE_AUTH_PROVIDER=mock on port 4175 before running this script.
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
const app = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.resolve(app,'../../Templates/aonik-admin-starterkit/marketing/screenshots');
const origin = process.env.TRANSFER_TEST_ORIGIN || 'http://127.0.0.1:4175';
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

  const tenant='11111111-1111-4111-8111-111111111111';
  const customer='22222222-2222-4222-8222-222222222222';
  const recipient='55555555-5555-4555-8555-555555555555';
  const destinationIds={'bank-demo':'33333333-3333-4333-8333-333333333333','mobile-demo':'66666666-6666-4666-8666-666666666666','wallet-demo':'77777777-7777-4777-8777-777777777777','legacy-demo':'88888888-8888-4888-8888-888888888888','new-bank':'99999999-9999-4999-8999-999999999999'};
  const mutations=[];
  const requests=[];
  let reader=false,registrations=0;
  const destination=(id,name,rail,currency,mask,verified=true)=>({destination:{externalPayoutAccountId:destinationIds[id],customerPartyId:customer,beneficiaryPartyId:recipient+id,beneficiaryName:name,destinationType:rail,maskedAccountIdentifier:mask,currency,bankCode:rail==='Bank'?'044':null,mobileNetwork:rail==='MobileMoney'?'MTN':null,accountName:name,providerCode:'Demo payout',relationshipTypeCode:'Recipient',isVerified:verified},notes:null});
  let items=[destination('bank-demo','Tayo Example','Bank','NGN','•••• 4821'),destination('mobile-demo','Esi Example','MobileMoney','GHS','•••• 6678'),destination('wallet-demo','Kofi Example','Wallet','GHS','•••• 8126'),destination('legacy-demo','Maya Example','Bank','NGN','•••• 9900',false)];
  await page('Page.addScriptToEvaluateOnNewDocument',{source:`localStorage.setItem('selected_tenant_id',${JSON.stringify(tenant)});localStorage.setItem('selected_tenant',JSON.stringify({tenantId:${JSON.stringify(tenant)},name:'Meridian Remit',environment:'Test'}));localStorage.setItem('aonik-theme','light');`});
  await page('Fetch.enable',{patterns:[{urlPattern:'*/api/*'}]});
  socket.addEventListener('message',async event=>{
    const message=JSON.parse(event.data);if(message.method!=='Fetch.requestPaused') return;
    const {requestId,request}=message.params;
    const route=new URL(request.url).pathname.replace(/^\/api/,'');requests.push(route);
    let code=200,data={items:[],totalCount:0};
    if(route==='/host/me/tenants') data={tenants:[{tenantId:tenant,name:'Meridian Remit',environment:'Test',roles:['Operations'],isActive:true}]};
    else if(route==='/admin/manifest') data={businessType:'remittance',enabledModules:['platform','ordering','finance','ai','agents','documents'],modules:[],featureFlags:{},permissions:reader?['Payment.Read','Customers.Read']:['Payment.Read','Payment.Create','Payment.Capture','Customers.Read','Customers.Write','Catalog.Read','Ledger.Read'],allowedPolicies:reader?['AdminUserPolicy','AdminReadPolicy']:['AdminUserPolicy','AdminReadPolicy','AdminWritePolicy','AdminPolicy','AdminUserWritePolicy']};
    else if(route==='/catalog/countries') data={countries:[{countryCode:'GB',name:'United Kingdom'},{countryCode:'NG',name:'Nigeria'},{countryCode:'GH',name:'Ghana'}]};
    else if(route==='/catalog/currencies') data={currencies:[{code:'GBP',name:'Pound Sterling'},{code:'NGN',name:'Nigerian Naira'},{code:'GHS',name:'Ghanaian Cedi'}]};
    else if(route===`/admin/customers/${customer}`) data={partyId:customer,displayName:'Nadia Example',partyType:'Person',status:'Active'};
    else if(route.startsWith('/admin/customers')) data={items:[{partyId:customer,displayName:'Nadia Example',partyType:'Person',status:'Active'}],totalCount:1,pageNumber:1,pageSize:25};
    else if(route===`/remittance/customers/${customer}/beneficiaries`) data={customerPartyId:customer,beneficiaries:items};
    else if(route==='/remittance/beneficiaries/verify') {
      registrations++;mutations.push({method:request.method,headers:request.headers,body:JSON.parse(request.postData)});
      if(registrations===1) {code=422;data={message:'Demo provider is temporarily unavailable. Please retry.'};}
      else {const value=JSON.parse(request.postData).beneficiary;const item=destination('new-bank',value.beneficiaryDisplayName,value.destinationType,value.currency,'•••• '+value.accountNumber.slice(-4));items.push(item);data=item.destination;code=201;}
    }
    else if(route.endsWith('/notes')) {mutations.push({method:request.method,headers:request.headers});const id=route.split('/').at(-2);const notes=JSON.parse(request.postData).notes;items=items.map(item=>item.destination.beneficiaryPartyId===id?{...item,notes}:item);code=204;}
    else if(request.method==='DELETE') {mutations.push({method:request.method,headers:request.headers});items=items.filter(item=>item.destination.externalPayoutAccountId!==route.split('/').at(-1));code=204;}
    else if(route.endsWith('/destinations')) data={customerPartyId:customer,beneficiaries:items.map(item=>item.destination)};
    else if(route==='/admin/notifications/summary') data={unreadCount:0};
    else if(route.includes('/notifications') || route.startsWith('/ai/agents')) data=[];
    try {await page('Fetch.fulfillRequest',{requestId,responseCode:code,responseHeaders:[{name:'Content-Type',value:'application/json'}],...(code===204?{}:{body:Buffer.from(JSON.stringify(data)).toString('base64')})});} catch { /* Navigation can cancel a request. */ }
  });
  const wait=async(expression,label)=>{const started=Date.now();while(!(await evaluate(expression))) {if(Date.now()-started>30000) throw new Error(`${label}: ${await evaluate('document.body.innerText.slice(-2000)')} Requests: ${JSON.stringify(requests.slice(-10))} Errors: ${errors.join(' | ')}`);await new Promise(resolve=>setTimeout(resolve,100));}};
  const click=async text=>evaluate(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b)throw Error('Missing button '+${JSON.stringify(text)});b.click();})()`);
  const input=async(id,value,textarea=false)=>evaluate(`(()=>{const e=document.getElementById(${JSON.stringify(id)}) || document.querySelector(${JSON.stringify(id)});Object.getOwnPropertyDescriptor(${textarea?'HTMLTextAreaElement':'HTMLInputElement'}.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  const select=async(id,value)=>evaluate(`(()=>{const e=document.getElementById(${JSON.stringify(id)}) || document.querySelector(${JSON.stringify(id)});e.value=${JSON.stringify(value)};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  const chooseCustomer=async()=>{
    await evaluate(`document.querySelector('[aria-label="Customer"]').click()`);
    await wait(`!![...document.querySelectorAll('button')].find(b=>b.textContent.includes('Nadia Example'))`,'Customer search failed');
    await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.includes('Nadia Example')).click()`);
    await wait(`document.querySelectorAll('tbody tr').length===${items.length} && document.body.innerText.includes('Tayo Example')`,'Beneficiary list failed');
  };
  const capture=async scene=>{
    for(const theme of ['light','dark']) {
      await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};document.activeElement?.blur()`);
      await evaluate(`new Promise(resolve=>setTimeout(resolve,350))`);
      const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
      await writeFile(path.join(output,`${scene}-${theme}@2x.png`),Buffer.from(result.data,'base64'));
    }
    await evaluate(`document.documentElement.dataset.theme='light'`);
  };
  await mkdir(output,{recursive:true});
  await page('Page.navigate',{url:origin+'/remittance/beneficiaries'});
  await wait(`!!document.querySelector('[aria-label="Customer"]')`,'Management page did not render');
  await evaluate(`document.querySelector('[aria-label="Toggle sidebar"]').click()`);
  await chooseCustomer();await capture('remittance-beneficiaries');
  if(!(await evaluate(`document.querySelector('[aria-label="Send transfer to Maya Example"]').disabled`))) throw new Error('Unverified destination can start a transfer');
  await evaluate(`document.querySelector('[aria-label="Send transfer to Tayo Example"]').click()`);
  await wait(`location.pathname==='/orders/transfers/new' && document.querySelector('#destination')?.value===${JSON.stringify(destinationIds['bank-demo'])} && document.querySelector('[aria-label="Sending customer"]').textContent.includes('Nadia Example')`,'Row Send transfer did not preselect customer/destination');
  if(await evaluate(`document.querySelector('#destination-currency').value!=='NGN' || document.querySelector('#transfer-amount').value!=='' || !document.body.innerText.includes('No transfer quote yet')`)) throw new Error('Selection started a quote or filled unrelated financial fields');
  await page('Page.reload');await wait(`document.querySelector('#destination')?.value===${JSON.stringify(destinationIds['bank-demo'])}`,'Transfer selection did not survive reload');
  await page('Page.navigate',{url:origin+'/orders/transfers/new?customerPartyId='+customer+'&destinationId=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'});
  await wait(`document.body.innerText.includes('selected payout destination is unavailable')`,'Missing destination was silently accepted');
  if(await evaluate(`document.querySelector('#destination').value!==''`)) throw new Error('Missing destination was selected');
  const storageKey='aonik:transfer-confirm:'+tenant+':mock-user-123';
  const recoveryAttempt={key:'existing-pending-send',request:{pricingQuoteId:'existing-quote',customerPartyId:customer,destinationExternalAccountId:destinationIds['mobile-demo'],purposeCode:'FamilySupport',narration:null,providerCode:null},quote:{pricingQuoteId:'existing-quote',originCurrency:'GBP',destinationCurrency:'GHS',originAmount:100,destinationAmount:1500,feesTotal:2,totalAmount:102,exchangeRate:15,feeBreakdown:[],pricingPolicyVersion:'v1',expiresAt:new Date(Date.now()-1000).toISOString()},maskedDestination:'Mobile money · •••• 6678'};
  await evaluate(`sessionStorage.setItem(${JSON.stringify(storageKey)},${JSON.stringify(JSON.stringify(recoveryAttempt))})`);
  const lookupsBefore=requests.filter(route=>route===`/admin/customers/${customer}`).length;
  await page('Page.navigate',{url:origin+'/orders/transfers/new?customerPartyId='+customer+'&destinationId='+destinationIds['bank-demo']});
  await wait(`document.body.innerText.includes('Confirmation pending')`,'Beneficiary link replaced pending recovery');
  if(requests.filter(route=>route===`/admin/customers/${customer}`).length!==lookupsBefore || await evaluate(`JSON.parse(sessionStorage.getItem(${JSON.stringify(storageKey)})).key!=='existing-pending-send'`)) throw new Error('Pending confirmation was replaced by link selection');
  await evaluate(`sessionStorage.removeItem(${JSON.stringify(storageKey)})`);
  await page('Page.navigate',{url:origin+'/remittance/beneficiaries'});await wait(`!!document.querySelector('[aria-label="Customer"]')`,'Management navigation failed');await chooseCustomer();
  await evaluate(`[...document.querySelectorAll('tbody tr')].find(row=>row.textContent.includes('Esi Example')).click()`);
  await wait(`!!document.querySelector('[data-slot="sheet-content"]')`,'Destination detail did not open');
  await evaluate(`[...document.querySelectorAll('[data-slot="sheet-footer"] button')].find(button=>button.textContent.trim()==='Send transfer').click()`);await wait(`document.querySelector('#destination')?.value===${JSON.stringify(destinationIds['mobile-demo'])} && document.querySelector('#destination-currency').value==='GHS'`,'Detail Send transfer did not select the mobile destination');
  await page('Page.navigate',{url:origin+'/remittance/beneficiaries'});await wait(`!!document.querySelector('[aria-label="Customer"]')`,'Management navigation failed');await chooseCustomer();
  await input('[aria-label="Search destinations"]','Kofi');
  await wait(`document.querySelectorAll('tbody tr').length===1 && document.querySelector('tbody').textContent.includes('Kofi')`,'Destination search failed');
  await input('[aria-label="Search destinations"]','');
  await select('[aria-label="Destination status"]','unverified');
  await wait(`document.querySelectorAll('tbody tr').length===1 && document.querySelector('tbody').textContent.includes('Maya')`,'Status filter failed');
  await select('[aria-label="Destination status"]','all');
  await click('Add destination');await wait(`!!document.getElementById('beneficiary-name')`,'Creation form missing');
  await input('beneficiary-name','Ada Example');await input('beneficiary-account-name','Ada Example');
  await select('beneficiary-country','NG');await select('beneficiary-currency','NGN');
  await input('beneficiary-bank-code','044');await input('beneficiary-account-number','0000004821');
  await select('beneficiary-rail','MobileMoney');await wait(`!!document.getElementById('beneficiary-phone') && !document.getElementById('beneficiary-account-number')`,'Mobile rail fields failed');
  await input('beneficiary-phone','+2348000004821');await select('beneficiary-rail','Wallet');await wait(`!!document.getElementById('beneficiary-wallet') && !document.getElementById('beneficiary-phone')`,'Wallet rail fields failed');
  await select('beneficiary-rail','Bank');
  if(await evaluate(`document.getElementById('beneficiary-account-number').value!==''`)) throw new Error('Changing rails retained raw account details');
  await input('beneficiary-bank-code','044');await input('beneficiary-account-number','0000004821');
  await capture('remittance-beneficiary-new');
  await click('Verify & save destination');await wait(`document.body.innerText.includes('Demo provider is temporarily unavailable')`,'Provider error missing');
  if(registrations!==1) throw new Error('Registration automatically retried');
  await click('Verify & save destination');await wait(`!document.getElementById('beneficiary-name') && document.body.innerText.includes('Ada Example')`,'Verified destination not listed');
  if(JSON.stringify(mutations[1].body)!==JSON.stringify(mutations[0].body)) throw new Error('Registration retry changed raw details');
  await evaluate(`[...document.querySelectorAll('tbody tr')].find(row=>row.textContent.includes('Tayo Example')).click()`);
  await wait(`document.body.innerText.includes('Destination reference')`,'Destination detail missing');
  await click('Edit recipient notes');await wait(`!!document.getElementById('recipient-notes')`,'Notes form missing');
  await input('recipient-notes','Monthly family support',true);await click('Save notes');await wait(`!document.getElementById('recipient-notes') && document.body.innerText.includes('Recipient notes updated')`,'Note edit failed');
  await evaluate(`[...document.querySelectorAll('tbody tr')].find(row=>row.textContent.includes('Tayo Example')).click()`);
  await wait(`document.body.innerText.includes('Monthly family support')`,'Updated note not projected');
  await evaluate(`document.querySelector('[data-slot="sheet-close"]').click()`);
  await wait(`!document.querySelector('[data-slot="sheet-content"]')`,'Detail did not close');
  const point=await evaluate(`(()=>{const bounds=[...document.querySelectorAll('tbody tr')].find(row=>row.textContent.includes('Ada Example')).querySelector('[aria-label="Row actions"]').getBoundingClientRect();return {x:bounds.x+bounds.width/2,y:bounds.y+bounds.height/2};})()`);
  await page('Input.dispatchMouseEvent',{type:'mousePressed',button:'left',clickCount:1,...point});
  await page('Input.dispatchMouseEvent',{type:'mouseReleased',button:'left',clickCount:1,...point});
  await wait(`!![...document.querySelectorAll('[role="menuitem"]')].find(item=>item.textContent==='Remove destination')`,'Row actions missing');
  await evaluate(`[...document.querySelectorAll('[role="menuitem"]')].find(item=>item.textContent==='Remove destination').click()`);
  await wait(`!!document.querySelector('[role="alertdialog"]')`,'Removal confirmation missing');
  await click('Remove destination');await wait(`!document.querySelector('[role="alertdialog"]') && !document.querySelector('tbody').textContent.includes('Ada Example')`,'Removal failed');
  for(const mutation of mutations) if(Object.entries(mutation.headers).find(([key])=>key.toLowerCase()==='x-tenant-id')?.[1]!==tenant) throw new Error('Mutation changed tenant');
  if(await evaluate(`JSON.stringify({...localStorage,...sessionStorage}).includes('0000004821')`)) throw new Error('Raw rail stored in browser storage');
  reader=true;await page('Page.reload');await wait(`!!document.querySelector('[aria-label="Customer"]')`,'Read-only route failed');await chooseCustomer();
  if(await evaluate(`!![...document.querySelectorAll('button')].find(b=>b.textContent==='Add destination' || b.textContent.includes('Send transfer'))`)) throw new Error('Read-only user sees mutations');
  if(errors.length) throw new Error(errors.join('\n'));
  await writeFile(path.join(output,'beneficiaries-report.json'),JSON.stringify({checks:['actual React UI','customer-scoped listing','row/detail Send transfer selects customer and destination','prefill survives reload','missing/unverified selection rejected','pending recovery takes precedence','shared icons and themes','search and status filters','conditional Bank/MobileMoney/Wallet fields','raw details cleared on rail change','explicit verification','provider error and manual retry','verified destination listed','notes save and readback','confirmed removal','captured tenant on mutations','no browser persistence of raw rails','read-only access','no runtime exceptions'],registrations},null,2)+'\n');
  console.log('Beneficiary browser checks passed. Four production-UI screenshots saved.');
} finally {
  socket?.close();chrome.kill();
  await new Promise(resolve=>chrome.exitCode!==null?resolve():chrome.once('exit',resolve));
  const resolved=path.resolve(userData);
  if(path.dirname(resolved)===tempRoot && path.basename(resolved).startsWith('aonik-showcase-')) await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
}
