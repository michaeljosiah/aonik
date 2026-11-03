// Runs the REAL Admin UI against intercepted, fictional API responses. No backend calls are sent.
// Start Vite with VITE_AUTH_PROVIDER=mock on port 4175 before running this script.
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { readFile, writeFile, mkdir, readdir, mkdtemp, rm } from 'node:fs/promises';
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
  const customer='22222222-2222-4222-8222-222222222222';
  const account='33333333-3333-4333-8333-333333333333';
  const orderId='44444444-4444-4444-8444-444444444444';
  let reader=false;
  let latestQuote;
  let detailReads=0,receiptMode='complete',failReceipt=false;
  let emailMode='lost-response';
  let historyMode='normal';
  let deliveryMode='awaiting';
  let savedCustomerEmail='nadia@example.test',recipientMode='normal';
  const recipientUpdates=[];
  const historyReads=[];
  const emailSends=[];
  const emailContact='77777777-7777-4777-8777-777777777777';
  const receiptFixtures={complete:await readFile(path.resolve(app,'../../output/pdf/remittance-transfer-receipt-example.pdf')),pending:await readFile(path.resolve(app,'../../tmp/pdfs/remittance-transfer-receipt-pending.pdf')),long:await readFile(path.resolve(app,'../../tmp/pdfs/remittance-transfer-receipt-long.pdf'))};
  const downloads=[],completedDownloads=new Set();
  socket.addEventListener('message',event=>{const data=JSON.parse(event.data);if(data.method==='Browser.downloadWillBegin') downloads.push(data.params);if(data.method==='Browser.downloadProgress' && data.params.state==='completed') completedDownloads.add(data.params.guid);});
  const downloadDirectory=path.join(userData,'receipt-downloads');await mkdir(downloadDirectory,{recursive:true});
  await call('Browser.setDownloadBehavior',{behavior:'allowAndName',downloadPath:downloadDirectory,eventsEnabled:true});
  let originalTransfer;
  const confirmations=[];
  const requests=[];
  const fixtureOrder=()=>({orderId,orderNumber:'REM-2026-1086',status:'Complete',customerPartyId:customer,beneficiaryPartyId:customer,destinationExternalAccountId:account,destinationType:'Bank',maskedAccountIdentifier:'•••• 4821',originCountry:'GB',destinationCountry:'NG',originCurrency:'GBP',destinationCurrency:'NGN',originAmount:250,destinationAmount:500000,feesTotal:3.50,totalAmount:253.50,exchangeRate:2000,pricingQuoteId:latestQuote?.pricingQuoteId || 'quote-demo',payoutId:'PAYOUT-DEMO-1086',providerCode:'Demo provider',clientReference:'REM-DEMO-1086',providerReference:'PROVIDER-DEMO-1086',transmissionStatus:'Succeeded',createdAt:'2026-11-01T12:00:00Z',submittedAt:'2026-11-01T12:00:01Z',settledAt:'2026-11-01T12:00:02Z'});
  await page('Page.addScriptToEvaluateOnNewDocument',{source:`localStorage.setItem('selected_tenant_id',${JSON.stringify(tenant)});localStorage.setItem('selected_tenant',JSON.stringify({tenantId:${JSON.stringify(tenant)},name:'Meridian Remit',environment:'Test'}));localStorage.setItem('aonik-theme','light');`});
  await page('Fetch.enable',{patterns:[{urlPattern:'*/api/*'}]});
  socket.addEventListener('message', async event=>{
    const message=JSON.parse(event.data);
    if(message.method!=='Fetch.requestPaused') return;
    const {requestId,request}=message.params;
    const uri=new URL(request.url);
    const route=uri.pathname.replace(/^\/api/,'');
    requests.push({route,method:request.method});
    let executionNotStarted=false;
    let binary;
    let status=200, data={items:[],totalCount:0,pageNumber:1,pageSize:20};
    if(route==='/host/me/tenants') data={tenants:[{tenantId:tenant,name:'Meridian Remit',environment:'Test',roles:['Operations'],isActive:true}]};
    else if(route==='/admin/manifest') data={businessType:'remittance',enabledModules:['platform','ordering','finance','ai','agents','documents'],modules:[],featureFlags:{},permissions:reader?['Payment.Read','Customers.Read']:['Payment.Read','Payment.Create','Payment.Capture','Customers.Read','Customers.Write','Catalog.Read','Ledger.Read'],allowedPolicies:reader?['AdminUserPolicy','AdminReadPolicy']:['AdminUserPolicy','AdminReadPolicy','AdminWritePolicy','AdminPolicy','AdminUserWritePolicy']};
    else if(route==='/catalog/countries') data={countries:[{countryCode:'GB',name:'United Kingdom'},{countryCode:'NG',name:'Nigeria'}]};
    else if(route==='/catalog/currencies') data={currencies:[{code:'GBP',name:'Pound Sterling'},{code:'NGN',name:'Nigerian Naira'}]};
    else if(route.startsWith('/admin/customers')) data={items:[{partyId:customer,displayName:'Nadia Example',partyType:'Person',status:'Active'}],totalCount:1,pageNumber:1,pageSize:25};
    else if(route.endsWith('/destinations')) data={customerPartyId:customer,beneficiaries:[{externalPayoutAccountId:account,customerPartyId:customer,beneficiaryPartyId:customer,beneficiaryName:'Tayo Example',destinationType:'Bank',maskedAccountIdentifier:'•••• 4821',currency:'NGN',accountName:'Tayo Example',providerCode:'Demo provider',isVerified:true},{externalPayoutAccountId:'unverified',customerPartyId:customer,beneficiaryPartyId:customer,beneficiaryName:'Unverified example',destinationType:'Bank',maskedAccountIdentifier:'•••• 9900',currency:'NGN',isVerified:false}]};
    else if(route==='/remittance/quote') {
      const body=JSON.parse(request.postData); const amount=body.originAmount || body.destinationAmount/2000;
      data=latestQuote={pricingQuoteId:crypto.randomUUID(),quoteType:'Remittance',originCountry:body.originCountry,destinationCountry:body.destinationCountry,originCurrency:body.originCurrency,destinationCurrency:body.destinationCurrency,originAmount:amount,destinationAmount:amount*2000,feesTotal:3.50,totalAmount:amount+3.50,exchangeRate:2000,rateMarkup:0.01,pricingPolicyId:'POLICY-DEMO-01',pricingPolicyVersion:'v1',fxRateId:'FX-DEMO-01',rateTimestamp:new Date().toISOString(),expiresAt:new Date(Date.now()+120000).toISOString(),feeBreakdown:[{code:'TRANSFER',description:'Transfer fee',amount:3.50,currency:'GBP',calculationType:'Fixed'}],supportedDestinationMethods:[{destinationType:'Bank',countryCode:'NG',currency:'NGN'}]};
    } else if(route==='/remittance/confirm') {
      confirmations.push({body:JSON.parse(request.postData),headers:request.headers});
      if(confirmations.length===1) {executionNotStarted=true;status=422;data={message:'Destination verification changed. Get a new quote.'};}
      else if(confirmations.length===2) {originalTransfer=confirmations[1];status=504;data={message:'Simulated lost confirmation response. Retry the original request.'};}
      else data=fixtureOrder();
    } else if(route===`/remittance/${orderId}`) {
      detailReads++;
      if(failReceipt) {status=403;data={message:'Receipt access was revoked.'};failReceipt=false;}
      else {data=fixtureOrder();if(detailReads===1 || receiptMode==='pending') data={...data,status:'Transmitted',transmissionStatus:'Pending',settledAt:null};if(receiptMode==='long') data={...data,providerReference:'LONG-REFERENCE-'.repeat(160)};}
    }
    else if(route===`/remittance/${orderId}/receipt`) binary=receiptFixtures[receiptMode];
    else if(route===`/remittance/${orderId}/receipt/email` && request.method==='GET') data={customerName:'Nadia Example',orderNumber:'REM-2026-1086',transferStatus:'Completed',fileName:'transfer-receipt-REM-2026-1086.pdf',contacts:emailMode==='no-contact'?[]:[{contactId:emailContact,email:emailMode==='updated-recipient'?'nadia.updated@example.test':savedCustomerEmail,isPrimary:true}]};
    else if(route===`/remittance/${orderId}/receipt/email` && request.method==='POST') {
      const body=JSON.parse(request.postData);emailSends.push({body,headers:request.headers,mode:emailMode});
      if(emailMode==='lost-response') {status=504;data={message:'Simulated lost email response.'};}
      else if(emailMode==='changed-recipient') {status=422;data={message:'Customer email address changed. Review the recipient again.'};}
      else data={requestId:body.requestId,status:emailMode==='failed'?'Failed':'Accepted',maskedRecipient:'n***@example.test',fileName:'transfer-receipt-REM-2026-1086.pdf',requestedAt:'2026-11-01T12:00:00Z'};
    }
    else if(route===`/remittance/${orderId}/receipt/email/history`) {
      const pageNumber=Number(uri.searchParams.get('pageNumber') || 1);historyReads.push({pageNumber,headers:request.headers});
      const recorded=new Map();
      for(const attempt of emailSends) if(attempt.mode!=='changed-recipient') recorded.set(attempt.body.requestId,{requestId:attempt.body.requestId,status:attempt.mode==='failed'?'Failed':'Accepted',maskedRecipient:'n***@example.test',fileName:'transfer-receipt-REM-2026-1086.pdf',requestedAt:'2026-11-01T12:00:00Z',statusUpdatedAt:'2026-11-01T12:00:02Z',deliveryStatus:attempt.mode==='failed'?'NotSent':deliveryMode==='delivered'?'Delivered':'AwaitingReport',deliveryUpdatedAt:deliveryMode==='delivered' && attempt.mode!=='failed'?'2026-11-01T12:01:00Z':null});
      let items=[...recorded.values()].reverse();
      if(historyMode==='showcase' || historyMode==='paged') items=Array.from({length:historyMode==='paged'?21:5},(_,i)=>({requestId:`88888888-8888-4888-8888-${String(i+1).padStart(12,'0')}`,status:['Accepted','Accepted','Failed','Unknown','Sending'][i%5],maskedRecipient:'n***@example.test',fileName:'transfer-receipt-REM-2026-1086.pdf',requestedAt:`2026-11-01T${String(12-i%10).padStart(2,'0')}:00:00Z`,statusUpdatedAt:i%5===4?null:`2026-11-01T${String(12-i%10).padStart(2,'0')}:00:02Z`,deliveryStatus:['Delivered','Bounced','NotSent','AwaitingReport','NotTracked'][i%5],deliveryUpdatedAt:i%5<2?`2026-11-01T${String(12-i%10).padStart(2,'0')}:01:00Z`:null}));
      if(historyMode==='empty') items=[];
      data={items:items.slice((pageNumber-1)*20,pageNumber*20),totalCount:items.length,pageNumber,pageSize:20};
      if(historyMode==='error') {status=503;data={message:'Receipt history is temporarily unavailable.'};}
    }
    else if(route.endsWith('/recipient') && route.startsWith(`/remittance/${orderId}/receipt/email/`)) {
      if(request.method==='GET') data={customerName:'Nadia Example',orderNumber:'REM-2026-1086',contact:{contactId:emailContact,email:savedCustomerEmail,isPrimary:true},changedSinceSend:savedCustomerEmail!=='nadia@example.test'};
      else {
        const body=JSON.parse(request.postData);recipientUpdates.push({body,headers:request.headers});
        if(recipientMode==='stale') {status=409;data={message:'The saved email changed. Reload and review it before updating.'};savedCustomerEmail='nadia.latest@example.test';}
        else {savedCustomerEmail=body.newEmail;data={contactId:emailContact,email:savedCustomerEmail,isPrimary:true};}
      }
    }
    else if(route.startsWith(`/remittance/${orderId}/receipt/email/`)) data={requestId:route.split('/').at(-1),status:'Accepted',maskedRecipient:'n***@example.test',fileName:'transfer-receipt-REM-2026-1086.pdf',requestedAt:'2026-11-01T12:00:00Z'};
    else if(route.startsWith('/ai/agents') || route.includes('/agents/available')) data=[];
    else if(route.includes('unread')) data={count:0,unreadCount:0};
    else if(route==='/admin/notifications/summary') data={unreadCount:0};
    else if(route==='/admin/notifications') data=[];
    else if(route.includes('/notifications')) data=[];
    try { await page('Fetch.fulfillRequest',{requestId,responseCode:status,responseHeaders:[{name:'Content-Type',value:binary?'application/pdf':'application/json'},...(executionNotStarted?[{name:'X-Transfer-Execution',value:'not-started'}]:[])],body:(binary || Buffer.from(JSON.stringify(data))).toString('base64')}); } catch { /* Navigation can cancel an intercepted request. */ }
  });
  const wait=async(expression,label)=>{const start=Date.now();while(!(await evaluate(expression))) {if(Date.now()-start>30000) throw new Error(`${label}: ${await evaluate('document.body.innerText.slice(0,1800)')} Requests: ${JSON.stringify(requests.slice(-15))} Errors: ${errors.join(' | ')}`);await new Promise(r=>setTimeout(r,100));}};
  const click=async text=>evaluate(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)});if(!b)throw Error('Missing button '+${JSON.stringify(text)});b.click();})()`);
  const input=async(id,value)=>evaluate(`(()=>{const e=document.getElementById(${JSON.stringify(id)});Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  const select=async(id,value)=>evaluate(`(()=>{const e=document.getElementById(${JSON.stringify(id)});e.value=${JSON.stringify(value)};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  await page('Page.navigate',{url:origin+'/orders/transfers/new'});
  await wait(`!!document.querySelector('[aria-label="Sending customer"]')`,'Editor did not render');
  await evaluate(`document.querySelector('[aria-label="Toggle sidebar"]').click();document.querySelector('[aria-label="Sending customer"]').click()`);
  await wait(`!![...document.querySelectorAll('button')].find(b=>b.textContent.includes('Nadia Example'))`,'Customer search failed');
  await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.includes('Nadia Example')).click()`);
  await wait(`document.querySelectorAll('#destination option').length===3`,'Destinations did not load');
  if(!(await evaluate(`document.querySelector('#destination option[value="unverified"]').disabled`))) throw new Error('Unverified rail is selectable');
  await select('destination',account);await select('origin-country','GB');await select('destination-country','NG');await select('origin-currency','GBP');
  await input('transfer-amount','250');await input('transfer-purpose','FamilySupport');
  await click('Get transfer quote');await wait(`document.body.innerText.includes('Quote available')`,'Quote failed');
  await input('transfer-amount','300');await wait(`document.body.innerText.includes('No transfer quote yet')`,'Changed amount did not invalidate quote');
  await input('transfer-amount','250');await click('Get transfer quote');await wait(`document.body.innerText.includes('Quote available')`,'Requote failed');
  await mkdir(output,{recursive:true});
  for(const theme of ['light','dark']) {
    await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)}`);
    // Theme foregrounds transition; capture only after they reach their final token values.
    await evaluate(`new Promise(resolve=>setTimeout(resolve,300))`);
    const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
    await writeFile(path.join(output,`remittance-transfer-editor-${theme}@2x.png`),Buffer.from(result.data,'base64'));
  }
  await evaluate(`document.documentElement.dataset.theme='light';document.querySelector('#transfer-reviewed').click()`);
  await click('Review & confirm transfer');await wait(`!!document.querySelector('[role="dialog"]')`,'Review dialog missing');
  await click('Confirm & send transfer');await wait(`document.body.innerText.includes('Destination verification changed') && document.body.innerText.includes('No transfer quote yet')`,'Definitive rejection did not unlock the editor');
  if(await evaluate(`document.querySelector('#transfer-amount').closest('fieldset').disabled`)) throw new Error('Rejected transfer remains locked');
  await click('Get transfer quote');await wait(`document.body.innerText.includes('Quote available')`,'Quote after rejection failed');
  await evaluate(`document.querySelector('#transfer-reviewed').click()`);await click('Review & confirm transfer');await click('Confirm & send transfer');
  await wait(`document.body.innerText.includes('Simulated lost confirmation response')`,'Lost response scenario missing');
  if(confirmations.length!==2) throw new Error('Confirmation was duplicated');
  await page('Page.reload');await wait(`document.body.innerText.includes('Confirmation pending')`,'Confirmation journal did not survive reload');
  if(!(await evaluate(`document.querySelector('#transfer-amount').disabled || document.querySelector('#transfer-amount').closest('fieldset').disabled`))) throw new Error('Uncertain transfer is editable');
  await evaluate(`document.querySelector('#transfer-reviewed').click()`);await click('Review & retry confirmation');await click('Retry same transfer');
  await wait(`location.pathname.endsWith(${JSON.stringify(orderId)}) && document.body.innerText.includes('Execution references')`,'Retry/detail navigation failed');
  if(confirmations.length!==3 || JSON.stringify(originalTransfer.body)!==JSON.stringify(confirmations[2].body)) throw new Error('Retry changed the transfer request');
  const header=(headers,name)=>Object.entries(headers).find(([key])=>key.toLowerCase()===name.toLowerCase())?.[1];
  if(header(originalTransfer.headers,'Idempotency-Key')!==header(confirmations[2].headers,'Idempotency-Key') || header(originalTransfer.headers,'X-Tenant-Id')!==tenant) throw new Error('Retry changed its key/tenant');
  if(await evaluate(`sessionStorage.getItem(${JSON.stringify('aonik:transfer-confirm:'+tenant+':mock-user-123')})!==null`)) throw new Error('Successful confirmation journal was not cleared');
  const waitDownload=async count=>{const started=Date.now();while(downloads.length<count || !completedDownloads.has(downloads[count-1].guid)) {if(Date.now()-started>20000) throw new Error('Receipt download failed: '+await evaluate('document.body.innerText.slice(0,2200)'));await new Promise(resolve=>setTimeout(resolve,100));}return readFile(path.join(downloadDirectory,downloads[count-1].guid));};
  if(!(await evaluate(`document.body.innerText.includes('Transmitted')`))) throw new Error('Initial execution snapshot fixture did not load');
  await click('Download receipt');const receipt=await waitDownload(1);
  if(downloads[0].suggestedFilename!=='transfer-receipt-REM-2026-1086.pdf' || receipt.subarray(0,5).toString()!=='%PDF-') throw new Error('Receipt is not a named PDF download');
  await wait(`document.body.innerText.includes('Complete') && !document.body.innerText.includes('Preparing receipt')`,'Receipt did not refresh its execution state');
  const receiptOutput=path.resolve(app,'../../output/pdf');await mkdir(receiptOutput,{recursive:true});
  await writeFile(path.join(receiptOutput,'remittance-transfer-receipt-example.pdf'),receipt);
  const proofOutput=path.resolve(app,'../../tmp/pdfs');await mkdir(proofOutput,{recursive:true});
  receiptMode='pending';await click('Download receipt');await writeFile(path.join(proofOutput,'remittance-transfer-pending.pdf'),await waitDownload(2));
  await wait(`!document.body.innerText.includes('Preparing receipt')`,'Pending receipt did not finish');
  failReceipt=true;await click('Download receipt');await wait(`document.body.innerText.includes('Receipt access was revoked')`,'Receipt permission error missing');
  if(downloads.length!==2) throw new Error('Receipt export fell back to cached transfer data after access refusal');
  receiptMode='long';await click('Download receipt');await writeFile(path.join(proofOutput,'remittance-transfer-long.pdf'),await waitDownload(3));
  receiptMode='complete';
  await click('Refresh status');await wait(`!document.body.innerText.includes('LONG-REFERENCE-') && document.body.innerText.includes('Execution references')`,'Normal receipt snapshot did not reload');
  await click('Send receipt');await wait(`!!document.querySelector('#receipt-email-contact')`,'Receipt recipients did not load');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Send receipt email').disabled`))) throw new Error('Receipt email does not require confirmation');
  if(await evaluate(`!!document.querySelector('[role="dialog"] input[type="email"]')`)) throw new Error('Arbitrary recipient entry is exposed');
  for(const theme of ['light','dark']) {
    await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,300))`);
    const screenshot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
    await writeFile(path.join(output,`remittance-send-receipt-${theme}@2x.png`),Buffer.from(screenshot.data,'base64'));
  }
  await evaluate(`document.documentElement.dataset.theme='light';document.querySelector('#receipt-email-confirm').click()`);
  await click('Send receipt email');await wait(`document.body.innerText.includes('Simulated lost email response')`,'Uncertain email response was not retained');
  const emailJournal=await evaluate(`JSON.parse(sessionStorage.getItem(${JSON.stringify('aonik:receipt-email:'+tenant+':mock-user-123:'+orderId)}))`);
  if(!emailJournal?.requestId || JSON.stringify(emailJournal).includes('nadia')) throw new Error('Email recovery references are missing or contain the raw recipient');
  await wait(`!!document.querySelector('[aria-label="Receipt email history"]') && document.querySelector('[aria-label="Receipt email history"]').textContent.includes(${JSON.stringify(emailJournal.requestId)})`,'Receipt history did not automatically refresh after sending');
  await page('Page.reload');await wait(`document.body.innerText.includes('Execution references')`,'Receipt page reload failed');
  await click('Send receipt');await wait(`document.body.innerText.includes('Check send status')`,'Email recovery journal was not restored');
  await click('Check send status');await wait(`document.body.innerText.includes('Receipt submitted for email delivery')`,'Accepted email status did not resolve uncertainty');
  if(emailSends.length!==1 || emailSends[0].body.requestId!==emailJournal.requestId || emailSends[0].body.confirmSend!==true || emailSends[0].body.contactId!==emailContact || header(emailSends[0].headers,'X-Tenant-Id')!==tenant) throw new Error('Email request was duplicated or changed its customer/tenant reference');
  if(Object.keys(emailSends[0].body).sort().join(',')!=='confirmSend,contactId,expectedEmail,requestId') throw new Error('Email request accepts browser-supplied receipt contents');
  emailMode='failed';await click('Send receipt');await wait(`!!document.querySelector('#receipt-email-contact')`,'Recipient reload failed');
  await evaluate(`document.querySelector('#receipt-email-confirm').click()`);await click('Send receipt email');
  await wait(`document.body.innerText.includes('The receipt was not sent')`,'Definitive provider rejection was not shown');await click('Close');
  emailMode='changed-recipient';await click('Send receipt');await wait(`!!document.querySelector('#receipt-email-contact')`,'Changed recipient fixture failed');
  await evaluate(`document.querySelector('#receipt-email-confirm').click()`);await click('Send receipt email');await wait(`document.body.innerText.includes('Customer email address changed')`,'Changed address rejection missing');await click('Close');
  emailMode='updated-recipient';await click('Send receipt');await wait(`document.body.innerText.includes('nadia.updated@example.test')`,'Updated saved address did not reload');
  await evaluate(`document.querySelector('#receipt-email-confirm').click()`);await click('Retry same request');await wait(`document.body.innerText.includes('Receipt submitted for email delivery')`,'Updated address could not reuse an unreserved reference');
  if(emailSends.at(-1).body.requestId!==emailSends.at(-2).body.requestId || emailSends.at(-1).body.expectedEmail!=='nadia.updated@example.test') throw new Error('Changed address started a replacement email reference');
  emailMode='no-contact';await click('Send receipt');await wait(`document.body.innerText.includes('no valid saved email address')`,'Missing saved email state absent');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Send receipt email').disabled`))) throw new Error('Email without saved address is enabled');
  await click('Close');
  const emailCountBeforeHistory=emailSends.length;
  historyMode='error';await click('Refresh history');await wait(`document.body.innerText.includes('Receipt history is temporarily unavailable')`,'History error feedback missing');
  if(await evaluate(`!!document.querySelector('[aria-label="Receipt email history"]')`)) throw new Error('Failed history request retained stale rows');
  historyMode='empty';await click('Refresh history');await wait(`document.body.innerText.includes('No receipt emails yet')`,'Empty receipt history missing');
  historyMode='showcase';await click('Refresh history');await wait(`!!document.querySelector('[aria-label="Receipt email history"]') && document.body.innerText.includes('Unconfirmed') && document.body.innerText.includes('Awaiting result')`,'History status variants missing');
  if(!(await evaluate(`['Delivered','Bounced','Awaiting report','Unavailable'].every(label=>document.querySelector('[aria-label="Receipt email history"]').textContent.includes(label))`))) throw new Error('Delivery status variants are missing');
  if(await evaluate(`(()=>{const container=document.querySelector('[aria-label="Receipt email history"]').parentElement;return container.scrollWidth>container.clientWidth+1;})()`)) throw new Error('History actions overflow at desktop width');
  for(const theme of ['light','dark']) {
    await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};document.querySelector('[aria-label="Receipt email history"]').closest('[data-slot="card"]').scrollIntoView({block:'center'});new Promise(resolve=>setTimeout(resolve,300))`);
    const screenshot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
    await writeFile(path.join(output,`remittance-receipt-history-${theme}@2x.png`),Buffer.from(screenshot.data,'base64'));
    await writeFile(path.join(output,`remittance-receipt-delivery-${theme}@2x.png`),Buffer.from(screenshot.data,'base64'));
  }
  await evaluate(`document.documentElement.dataset.theme='light'`);
  historyMode='paged';await click('Refresh history');await wait(`document.body.innerText.includes('Page 1 of 2')`,'History page count missing');
  await click('Next');await wait(`document.body.innerText.includes('Page 2 of 2') && document.querySelectorAll('[aria-label="Receipt email history"] tbody tr').length===1`,'History next page failed');
  await click('Previous');await wait(`document.body.innerText.includes('Page 1 of 2')`,'History previous page failed');
  if(emailSends.length!==emailCountBeforeHistory || !historyReads.some(read=>read.pageNumber===2) || historyReads.some(read=>header(read.headers,'X-Tenant-Id')!==tenant)) throw new Error('History reads sent an email or lost tenant/page context');
  historyMode='showcase';
  await click('Refresh history');await wait(`document.body.innerText.includes('Page 1 of 1')`,'Resend history fixture did not load');
  if(await evaluate(`document.querySelectorAll('[aria-label^="Resend receipt for request"]:disabled').length`)!==2) throw new Error('Uncertain history sends allow a replacement copy');
  emailMode='accepted';
  const originalReceiptRequest='88888888-8888-4888-8888-000000000001';
  const openResend=async reference=>{await evaluate(`document.querySelector(${JSON.stringify('[aria-label="Resend receipt for request '+reference+'"]')}).click()`);await wait(`!!document.querySelector('[role="dialog"]') && document.body.innerText.includes('Resend transfer receipt') && !!document.querySelector('#receipt-email-contact')`,'Resend recipient dialog did not load');};
  await openResend(originalReceiptRequest);
  if(await evaluate(`document.querySelector('#receipt-email-contact').value`)!=='') throw new Error('A masked historical recipient was guessed');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Confirm & resend receipt').disabled`))) throw new Error('Resend bypasses recipient confirmation');
  await click('Close');
  if(emailSends.length!==emailCountBeforeHistory) throw new Error('Opening or cancelling a resend sent an email');
  await openResend(originalReceiptRequest);await select('receipt-email-contact',emailContact);
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Confirm & resend receipt').disabled`))) throw new Error('Recipient selection bypasses resend confirmation');
  await evaluate(`document.querySelector('#receipt-email-confirm').click()`);
  for(const theme of ['light','dark']) {
    await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,300))`);
    const screenshot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
    await writeFile(path.join(output,`remittance-resend-receipt-${theme}@2x.png`),Buffer.from(screenshot.data,'base64'));
  }
  await evaluate(`document.documentElement.dataset.theme='light'`);
  const previousReferences=emailSends.map(attempt=>attempt.body.requestId);
  emailMode='accepted';historyMode='normal';await click('Confirm & resend receipt');await wait(`document.body.innerText.includes('Receipt submitted for email delivery') && !document.querySelector('[role="dialog"]')`,'Confirmed resend failed');
  const resentRequest=emailSends.at(-1).body;
  if(resentRequest.requestId===originalReceiptRequest || previousReferences.includes(resentRequest.requestId) || !resentRequest.confirmSend || resentRequest.expectedEmail!=='nadia@example.test') throw new Error('Resend did not create a separately confirmed send reference');
  await wait(`!!document.querySelector(${JSON.stringify('[aria-label="Resend receipt for request '+resentRequest.requestId+'"]')})`,'Resend did not appear as a new history entry');
  emailMode='lost-response';await openResend(resentRequest.requestId);await select('receipt-email-contact',emailContact);await evaluate(`document.querySelector('#receipt-email-confirm').click()`);await click('Confirm & resend receipt');
  await wait(`document.body.innerText.includes('Simulated lost email response')`,'Lost resend response was not retained');
  const uncertainResend=emailSends.at(-1).body.requestId;
  const sendsAfterLostResend=emailSends.length;
  await click('Close');await openResend(resentRequest.requestId);await wait(`document.body.innerText.includes('Resolve the earlier request before sending another copy')`,'An unresolved send did not block a replacement resend');
  await select('receipt-email-contact',emailContact);await evaluate(`document.querySelector('#receipt-email-confirm').click()`);
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Confirm & resend receipt').disabled`))) throw new Error('Uncertain resend can start another copy');
  await click('Check send status');await wait(`!document.querySelector('[role="dialog"]') && document.body.innerText.includes('Receipt submitted for email delivery')`,'Uncertain resend status did not recover');
  if(emailSends.length!==sendsAfterLostResend || !requests.some(request=>request.route.endsWith('/receipt/email/'+uncertainResend))) throw new Error('Checking a resend started another email');
  deliveryMode='delivered';await click('Refresh history');await wait(`document.querySelector('[aria-label="Receipt email history"]')?.textContent.includes('Delivered')`,'Delivery reports did not refresh');
  if(emailSends.length!==sendsAfterLostResend) throw new Error('Refreshing delivery reports sent an email');
  historyMode='showcase';await click('Refresh history');await wait(`!!document.querySelector('[aria-label^="Review customer email for request"]')`,'Bounced email review action is missing');
  const beforeReviewSends=emailSends.length;
  const openEmailReview=async()=>{await evaluate(`document.querySelector('[aria-label^="Review customer email for request"]').click()`);await wait(`!!document.getElementById('receipt-review-email')`,'Saved contact review did not load');};
  await openEmailReview();
  if(await evaluate(`document.querySelector('#receipt-review-email').value`)!=='nadia@example.test') throw new Error('Bounced review did not load the saved contact');
  await click('Cancel');
  if(recipientUpdates.length || emailSends.length!==beforeReviewSends) throw new Error('Opening or cancelling email review changed customer data or sent email');
  await openEmailReview();await input('receipt-review-email','invalid');await evaluate(`document.querySelector('#receipt-review-confirm').click()`);await click('Save email & review receipt');
  if(recipientUpdates.length) throw new Error('Invalid email was submitted');
  await input('receipt-review-email','nadia.corrected@example.test');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Save email & review receipt').disabled`))) throw new Error('Changing email retained update confirmation');
  await evaluate(`document.querySelector('#receipt-review-confirm').click()`);
  for(const theme of ['light','dark']) {
    await evaluate(`document.documentElement.dataset.theme=${JSON.stringify(theme)};new Promise(resolve=>setTimeout(resolve,300))`);
    const screenshot=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
    await writeFile(path.join(output,`remittance-review-receipt-email-${theme}@2x.png`),Buffer.from(screenshot.data,'base64'));
  }
  await evaluate(`document.documentElement.dataset.theme='light'`);
  if(await evaluate(`(()=>{const d=document.querySelector('[role="dialog"]');return d.scrollWidth>d.clientWidth+1 || [...d.querySelectorAll('button,input')].some(e=>e.getBoundingClientRect().right>d.getBoundingClientRect().right-8);})()`)) throw new Error('Email review controls overflow the dialog');
  recipientMode='stale';await click('Save email & review receipt');await wait(`document.body.innerText.includes('The saved email changed.')`,'Stale saved email feedback missing');
  if(emailSends.length!==beforeReviewSends) throw new Error('Saving a stale contact sent an email');
  await click('Reload saved email');await wait(`document.querySelector('#receipt-review-email')?.value==='nadia.latest@example.test'`,'Reload did not fetch the current saved email');
  if(!(await evaluate(`document.body.innerText.includes('changed since the bounced receipt')`))) throw new Error('Changed-since-send warning missing');
  await input('receipt-review-email','nadia.corrected@example.test');await evaluate(`document.querySelector('#receipt-review-confirm').click()`);
  recipientMode='normal';await click('Save email & review receipt');await wait(`!!document.querySelector('#receipt-email-contact')`,'Saved email did not continue to resend');
  if(emailSends.length!==beforeReviewSends || recipientUpdates.length!==2) throw new Error('Updating the email dispatched a receipt');
  if(await evaluate(`document.querySelector('#receipt-email-contact').value`)!=='') throw new Error('Email update bypassed saved recipient selection');
  if(!(await evaluate(`document.querySelector('#receipt-email-contact').textContent.includes('nadia.corrected@example.test')`))) throw new Error('Resend did not reload the corrected saved contact');
  await select('receipt-email-contact',emailContact);
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Confirm & resend receipt').disabled`))) throw new Error('Email update bypassed resend confirmation');
  await evaluate(`document.querySelector('#receipt-email-confirm').click()`);
  emailMode='accepted';historyMode='normal';await click('Confirm & resend receipt');await wait(`!document.querySelector('[role="dialog"]') && document.body.innerText.includes('Receipt submitted for email delivery')`,'Corrected recipient resend did not complete');
  const correctedSend=emailSends.at(-1).body;
  if(emailSends.length!==beforeReviewSends+1 || correctedSend.expectedEmail!=='nadia.corrected@example.test' || correctedSend.requestId==='88888888-8888-4888-8888-000000000002') throw new Error('Corrected resend did not use a fresh request and saved email');
  if(recipientUpdates.some(update=>header(update.headers,'X-Tenant-Id')!==tenant || !update.body.confirmUpdate)) throw new Error('Customer email update lost tenant context or confirmation');
  historyMode='showcase';
  reader=true;await page('Page.reload');await wait(`document.body.innerText.includes('Execution references')`,'Read-only transfer detail failed');
  await wait(`!!document.querySelector('[aria-label="Receipt email history"]')`,'Read-only staff cannot view receipt history');
  if(await evaluate(`!![...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Send receipt')`)) throw new Error('Read-only user can send receipts');
  if(await evaluate(`document.querySelectorAll('[aria-label^="Resend receipt for request"]').length`)) throw new Error('Read-only user can resend receipts');
  if(await evaluate(`!![...document.querySelectorAll('nav a')].find(a=>a.getAttribute('href')==='/orders/transfers/new')`)) throw new Error('Read-only navigation exposes creation');
  if(await evaluate(`document.querySelectorAll('[aria-label^="Review customer email for request"]').length`)) throw new Error('Read-only user can review or update a bounced contact');
  if(errors.length) throw new Error(errors.join('\n'));
  await writeFile(path.join(output,'transfer-editor-report.json'),JSON.stringify({checks:['Actual Admin UI renders','customer search','unverified rail disabled','server quote','changed amount invalidates quote','light/dark screenshots','explicit confirmation dialog','definitive rejection unlocks and requotes','no duplicate submit','lost-response journal survives reload','locked uncertain request','same body/key/tenant on retry','read-only detail','actual named PDF receipt download','fresh execution snapshot for receipt','pending receipt','long-reference receipt pagination','permission refusal prevents cached export','no runtime exceptions'],confirmations:confirmations.length},null,2)+'\n');
  await writeFile(path.join(output,'receipt-email-report.json'),JSON.stringify({checks:['saved recipients only','explicit confirmation','shared communication icon','light/dark screenshots','lost-response journal survives reload without raw email','status check does not resend','captured tenant and stable reference','no browser-supplied PDF or amounts','provider rejection feedback','changed saved address reuses the original unreserved reference','no saved email state','read-only action hidden'],emailRequests:emailSends.length},null,2)+'\n');
  await writeFile(path.join(output,'receipt-history-report.json'),JSON.stringify({checks:['automatic history refresh after send','masked historical recipients','UTC request and outcome times','four honest send-status labels','failure clears stale rows','empty history','refresh retries without sending','next/previous server pages','captured tenant context','read-only staff history','light/dark captures'],historyReads:historyReads.length,emailRequests:emailSends.length},null,2)+'\n');
  await writeFile(path.join(output,'receipt-resend-report.json'),JSON.stringify({checks:['history row opens recipient confirmation','masked recipient is not guessed','cancellation does not send','saved recipient and checkbox both required','new send reference for another copy','history refresh after resend','uncertain historical rows disabled','pending tab request prevents another copy','lost resend status check without another email','read-only actions hidden','light/dark captures'],emailRequests:emailSends.length},null,2)+'\n');
  await writeFile(path.join(output,'receipt-email-review-report.json'),JSON.stringify({checks:['bounced row action only','original saved contact loaded','cancellation is read-only','invalid email blocked','update confirmation resets after edit','stale email conflict and reload','changed since send warning','save does not send','resend reloads corrected contact','fresh recipient selection and send confirmation','new send reference','captured tenant and permissions','read-only actions hidden','light/dark screenshots'],contactUpdates:recipientUpdates.length},null,2)+'\n');
  await writeFile(path.join(output,'receipt-delivery-report.json'),JSON.stringify({checks:['delivered and bounced provider badges','waiting and unavailable older sends','separate send and delivery timestamps','shared delivery SVGs','read-only history','delivery refresh does not send','light/dark captures'],emailRequests:emailSends.length},null,2)+'\n');
  console.log('Transfer editor, PDF download and receipt email browser checks passed. No real email sent.');
} finally {
  socket?.close();chrome.kill();
  await new Promise(resolve=>chrome.exitCode!==null?resolve():chrome.once('exit',resolve));
  const resolved=path.resolve(userData);
  if(path.dirname(resolved)===tempRoot && path.basename(resolved).startsWith('aonik-showcase-')) await rm(resolved,{recursive:true,force:true,maxRetries:5,retryDelay:200}).catch(()=>{});
}
