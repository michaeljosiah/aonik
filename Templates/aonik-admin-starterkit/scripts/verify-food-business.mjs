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
  const nav=async scene=>{await page('Page.navigate',{url:origin+'/food-business.html?capture=1&scene='+scene});await wait(`!!document.querySelector('[data-food-scene="${scene}"]')`,'Food scene did not load: '+scene);await evaluate('document.fonts.ready.then(()=>true)');await evaluate(`Promise.all([...document.querySelectorAll(".food-design img")].map(img=>img.decode())).then(()=>true)`);};
  const click=label=>evaluate(`(()=>{const scope=document.querySelector('.fd-dialog')||document.querySelector('.food-design');const b=[...scope.querySelectorAll('button')].find(b=>(b.textContent.trim()===${JSON.stringify(label)}||b.getAttribute('aria-label')===${JSON.stringify(label)}));if(!b)throw Error('Missing button '+${JSON.stringify(label)});b.click();return true;})()`);
  const aria=label=>evaluate(`(()=>{const e=document.querySelector('[aria-label="'+${JSON.stringify(label)}+'"]');if(!e)throw Error('Missing '+${JSON.stringify(label)});e.click();return true;})()`);
  const input=async(selector,value)=>evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});Object.getOwnPropertyDescriptor(e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));return true;})()`);
  const select=async(label,value)=>evaluate(`(()=>{const e=document.querySelector('[aria-label="'+${JSON.stringify(label)}+'"]');e.value=${JSON.stringify(value)};e.dispatchEvent(new Event('change',{bubbles:true}));return true;})()`);
  const shot=async file=>{const result=await page('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(output,file),Buffer.from(result.data,'base64'));};
  const capture=async prefix=>{let light;for(const theme of ['light','dark']){const bg=await evaluate(`(async()=>{document.documentElement.dataset.theme=${JSON.stringify(theme)};await new Promise(resolve=>setTimeout(resolve,350));return getComputedStyle(document.documentElement).getPropertyValue('--background');})()`);if(theme==='light')light=bg;else if(bg===light)throw Error('Food themes did not differ');await shot(`${prefix}-${theme}@2x.png`);}};
  await mkdir(output,{recursive:true});
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1100,deviceScaleFactor:2,mobile:false});
  await nav('today');
  const scenes=await evaluate('FOOD_SCENES.map(scene=>({id:scene.id,legacy:scene.legacy}))');
  if(scenes.length!==41)throw Error('Incomplete Food Business inventory');
  for(const {id} of scenes){
    await nav(id);
    const overflow=await evaluate(`[...document.querySelectorAll('.food-design,.fd-main,.fd-content,.fd-table-wrap,.fd-form-card')].filter(e=>e.scrollWidth>e.clientWidth+1).map(e=>e.className)`);
    if(overflow.length)throw Error('Horizontal overflow in '+id+': '+overflow.join(', '));
    if(await evaluate(`document.querySelectorAll('[aria-label="Food business navigation"]>button').length`)!==8)throw Error('Primary food navigation has grown beyond eight destinations');
    if(await evaluate(`document.querySelectorAll('.food-design .fd-primary').length`)!==1)throw Error('Page has competing primary actions');
    if(await evaluate(`!!document.querySelector('.food-design img:not([data-agent-portrait])')`))throw Error('Screenshot-backed food layout');
    await capture('food-'+id);
  }
  await nav('customer');
  const customerTabs=await evaluate(`[...document.querySelectorAll('[aria-label="Customer detail views"] [role=tab]')].map(tab=>tab.textContent)`);
  if(customerTabs.join(',')!=='Overview,Orders,Commerce,Finance,Insights,Documents,Activity')throw Error('Customer tabs diverged from the shared detail structure');
  if(!(await evaluate(`document.querySelector('.fd-customer-detail').textContent.includes('Addresses & preferences') && document.querySelector('.fd-customer-detail').textContent.includes('Not subscribed')`)))throw Error('Customer overview lacks profile, address or consent detail');
  await click('Add internal note');await input('[aria-label="Customer internal note"]','Customer prefers the doorbell for delivery.');await click('Save internal note');
  await aria('Customer Activity tab');
  if(!(await evaluate(`document.querySelector('.fd-customer-activity').textContent.includes('Customer prefers the doorbell') && document.querySelector('.fd-customer-activity').textContent.includes('Oliver Reed')`)))throw Error('Customer note has no scoped activity record');
  await evaluate(`[...document.querySelectorAll('[aria-label="Filter customer activity"] button')].find(b=>b.textContent==='Finance').click();true`);
  if(!(await evaluate(`[...document.querySelectorAll('[data-customer-event]')].every(e=>e.dataset.customerEvent==='finance') && document.querySelectorAll('[data-customer-event]').length===2`)))throw Error('Customer activity filter does not isolate finance');
  await aria('Customer Finance tab');await click('Payments');await aria('Open PAY-3048');
  if(!(await evaluate(`document.querySelector('[data-food-scene="payment"]').textContent.includes('PAY-3048')`)))throw Error('Customer finance payment deep link failed');
  await click('Customers');await aria('Open Alex Morgan');await aria('Customer Finance tab');await click('Payments');
  if(!(await evaluate(`document.querySelector('.fd-customer-detail').textContent.includes('No successful payments') && !document.querySelector('.fd-customer-detail').textContent.includes('PAY-3048')`)))throw Error('Customer finance leaked another customer payment');
  await aria('Customer Overview tab');
  if(!(await evaluate(`!document.querySelector('.fd-customer-detail').textContent.includes('Customer prefers the doorbell')`)))throw Error('Internal notes leaked between customers');
  await aria('Customer Documents tab');await aria('Open Delivery instructions.pdf');
  if(!(await evaluate(`document.querySelector('[aria-label="Customer document preview"]').textContent.includes('CUS-1004')`)))throw Error('Document preview lost customer scope');
  await aria('Close customer document');await aria('Customer Commerce tab');
  if(!(await evaluate(`document.querySelector('.fd-customer-detail').textContent.includes('Guest checkout') && document.querySelector('.fd-customer-detail').textContent.includes('ORD-2045')`)))throw Error('Commerce tab did not use selected customer');
  await aria('Customer Insights tab');
  if(!(await evaluate(`document.querySelector('.fd-customer-detail').textContent.includes('INV-1045') && document.querySelector('.fd-customer-detail').textContent.includes('insufficient to infer')`)))throw Error('Customer insight is ungrounded');
  await click('Customers');await aria('Open Nadia Mensah');await aria('Customer Finance tab');await click('Credit notes');await aria('Open CN-0048');
  if(!(await evaluate(`document.querySelector('[aria-label="Customer document preview"]').textContent.includes('No credit has been issued')`)))throw Error('Draft credit was represented as posted');
  await aria('Close customer document');
  await evaluate(`document.querySelector('[aria-label="Customer Finance tab"]').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowRight',bubbles:true}));true`);
  if(!(await evaluate(`document.querySelector('[aria-label="Customer Insights tab"]').getAttribute('aria-selected')==='true'`)))throw Error('Customer tab keyboard navigation failed');
  await nav('customers');await input('[aria-label="Search food records"]','Nadia');await aria('Open Nadia Mensah');
  if(!(await evaluate(`document.querySelector('[data-food-scene="customer"]').textContent.includes('CUS-1001') && document.querySelector('.fd-metrics').textContent.includes('£42.00')`)))throw Error('Customer account did not preserve selected record');
  await click('View customer order');
  if(!(await evaluate(`document.querySelector('.fd-record-heading').textContent.includes('ORD-2048')`)))throw Error('Customer to order link failed');
  await click('View invoice');
  if(!(await evaluate(`document.querySelector('.fd-invoice-sheet').textContent.includes('INV-1048') && document.querySelector('.fd-invoice-sheet').textContent.includes('Nadia Mensah')`)))throw Error('Order to invoice link failed');
  await evaluate(`[...document.querySelectorAll('.fd-record-links button')].find(b=>b.textContent.includes('PAY-3048')).click();true`);
  if(!(await evaluate(`document.querySelector('[data-food-scene="payment"]').textContent.includes('PSP-80348')`)))throw Error('Invoice to payment link failed');
  await click('View ledger entries');
  if(!(await evaluate(`document.querySelector('[data-food-scene="accounting"]').textContent.includes('Trade receivables') && document.querySelector('[data-food-scene="accounting"]').textContent.includes('Payment clearing')`)))throw Error('Payment has no ledger evidence');
  await click('Customers');await aria('Open Alex Morgan');await click('View customer order');
  if(!(await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Open packing for this order').disabled`)))throw Error('Unpaid order details allow packing');
  await click('View invoice');await evaluate(`[...document.querySelectorAll('.fd-record-links button')].find(b=>b.textContent.includes('Awaiting payment')).click();true`);
  if(!(await evaluate(`document.querySelector('.fd-dialog').textContent.includes('no successful payment allocation')`)))throw Error('Unpaid invoice fabricated a payment');
  await click('Close');await aria('Search AONIK workspace');await input('[aria-label="Search workspace records"]','INV-1046');
  await evaluate(`document.querySelector('.fd-search-results button').click();true`);
  if(!(await evaluate(`document.querySelector('.fd-invoice-sheet').textContent.includes('Priya Shah')`)))throw Error('Global search lost record identity');
  await aria('Workspace notifications');await evaluate(`[...document.querySelectorAll('.fd-search-results button')].find(b=>b.textContent.includes('Payment follow-up')).click();true`);
  if(!(await evaluate(`document.querySelector('.fd-invoice-sheet').textContent.includes('Alex Morgan')`)))throw Error('Notification failed to open its invoice');
  await click('Reports');
  if(!(await evaluate(`document.querySelector('.fd-metrics').textContent.includes('£180.00') && document.querySelector('.fd-metrics').textContent.includes('£156.00') && document.querySelector('.fd-metrics').textContent.includes('£24.00')`)))throw Error('Sales report does not reconcile orders, receipts and balances');
  await input('[aria-label="Search food records"]','Alex');
  if(!(await evaluate(`document.querySelectorAll('[data-food-row]').length===1 && document.querySelector('.fd-metrics').textContent.includes('£24.00') && document.querySelector('.fd-metrics').textContent.includes('£0.00')`)))throw Error('Filtered sales totals do not follow displayed rows');
  await call('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:userData});
  await click('Export report');
  if(!(await evaluate(`document.body.innerText.includes('Sales report exported')`)))throw Error('Sales export not confirmed');
  let exported='';for(let attempt=0;attempt<40;attempt++){try{exported=await readFile(path.join(userData,'abbys-table-sales-2026-11-03.csv'),'utf8');break;}catch{await new Promise(resolve=>setTimeout(resolve,100));}}
  if(!exported.includes('Alex Morgan')||exported.includes('Nadia Mensah')||!exported.includes('\"GBP\",\"24\",\"0\",\"24\"'))throw Error('Exported CSV does not match the filtered sales report');
  await select('Preview workspace role','kitchen');
  if(!(await evaluate(`document.querySelectorAll('[aria-label="Food business navigation"]>button').length===4 && !document.querySelector('[aria-label="Finance"]') && !document.querySelector('[aria-label="Search AONIK workspace"]') && !!document.querySelector('[data-food-scene="plan"]')`)))throw Error('Kitchen role preview includes owner destinations');
  await select('Preview workspace role','owner');
  if(!(await evaluate(`document.querySelectorAll('[aria-label="Food business navigation"]>button').length===8 && !!document.querySelector('[data-food-scene="today"]')`)))throw Error('Owner role preview was not restored');
  await nav('ingredients');await input('[aria-label="Search food records"]','rice');
  if(await evaluate(`document.querySelectorAll('[data-food-row]').length`)!==1)throw Error('Food list search failed');
  await nav('batches');await click('View release check');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(b=>b.textContent.trim()==='Release batch').disabled && document.querySelector('.fd-dialog').textContent.includes('No ingredient stock has changed')`)))throw Error('Insufficient-stock release is not blocked');
  await capture('food-release-blocked');await click('Cancel');await click('Review shortfall');
  if(!(await evaluate(`document.querySelector('[data-food-row="rice"]').textContent.includes('3 kg')`)))throw Error('Shortfall did not net reserved stock');
  await click('Create purchase order');await select('Shortfall supplier','Lagos Wholesale');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(b=>b.textContent.trim()==='Create draft order').disabled`)))throw Error('Foreign-currency supplier comparison is allowed');
  await select('Shortfall supplier','Greenfield Foods');await click('Create draft order');await aria('Open PO-2041');await click('Submit order');
  if(!(await evaluate(`document.querySelector('[data-food-row="PO-2041"]').textContent.includes('Pending')`)))throw Error('Purchase order submit uses the wrong status');
  await aria('Open PO-2041');await click('Receive delivery');
  await input('[aria-label="Receive rice quantity"]','6');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-form-actions button')].find(b=>b.textContent.trim()==='Review receipt').disabled`)))throw Error('Over-receipt is allowed');
  await input('[aria-label="Receive rice quantity"]','5');await click('Review receipt');await aria('Confirm goods receipt');await click('Record receipt');
  if(!(await evaluate(`document.body.innerText.includes('Receipt recorded once') && [...document.querySelectorAll('.fd-form-actions button')].find(b=>b.textContent.trim()==='Receipt recorded').disabled`)))throw Error('Receipt is not single-application');
  await click('Stock & purchasing');
  if(!(await evaluate(`document.querySelector('[data-food-row="rice"]').textContent.includes('15 kg') && document.querySelector('[data-food-row="rice"]').textContent.includes('10 kg')`)))throw Error('Receipt did not update on-hand and available stock');
  await click('Kitchen');await click('Batches');await click('Review release');await aria('Confirm batch release');await click('Release batch');
  await click('Stock & purchasing');
  if(!(await evaluate(`document.querySelector('[data-food-row="rice"]').textContent.includes('7 kg') && document.querySelector('[data-food-row="rice"]').textContent.includes('2 kg')`)))throw Error('Release did not consume the saved quantities once');
  await click('Kitchen');await click('Batches');await click('Cancel batch');await click('Cancel batch');await click('Stock & purchasing');
  if(!(await evaluate(`document.querySelector('[data-food-row="rice"]').textContent.includes('7 kg')`)))throw Error('Cancelling a released batch automatically restocked it');
  await nav('receiving');await click('Review receipt');await aria('Confirm goods receipt');await click('Record receipt');await click('Kitchen');await click('Batches');await click('Review release');await aria('Confirm batch release');await click('Release batch');await click('Start cooking');await click('Start cooking');await click('Record completion');await input('[aria-label="Actual cooked portions"]','38');await aria('Add finished yield to stock');await click('Complete batch');await click('Stock & purchasing');
  if(!(await evaluate(`document.querySelector('[data-food-row="rice"]').textContent.includes('7 kg')`)))throw Error('Completion consumed ingredients twice');
  await click('Finished dishes');
  if(!(await evaluate(`document.querySelector('[data-food-row="jollof"]').textContent.includes('70 portions')`)))throw Error('Explicit finished yield was not posted');
  await nav('recipes');await aria('Open Jollof rice & chicken');await input('[aria-label="Recipe rice quantity"]','2.5');await click('Save recipe');await click('Kitchen');await click('Batches');await click('Kitchen sheet ↗');
  if(!(await evaluate(`document.querySelector('[data-food-row="rice"]').textContent.includes('8 kg') && document.body.innerText.includes('saved batch has not')`)))throw Error('Recipe edit changed the frozen kitchen sheet');
  await nav('batches');await click('All batches ↗');await click('New batch');await input('[aria-label="New batch portions"]','20');await aria('Confirm recipe snapshot');await click('Create planned batch');
  if(!(await evaluate(`document.querySelector('.fd-dialog').textContent.includes('KT-1103-2')`)))throw Error('New batch draft did not appear in the batch list');
  await aria('Open KT-1103-2');
  if(!(await evaluate(`document.querySelector('.fd-dialog').textContent.includes('4 kg') && document.querySelector('.fd-dialog').textContent.includes('3.2 kg')`)))throw Error('New batch did not save its recipe quantities');
  await aria('Close food review');
  await nav('receiving');await input('[aria-label="Receive rice quantity"]','1');await click('Review receipt');await aria('Confirm goods receipt');await click('Record receipt');
  if(!(await evaluate(`document.querySelector('.fd-form-card').textContent.includes('11 kg') && document.querySelector('.fd-form-card').textContent.includes('Pending')`)))throw Error('Partial receipt incorrectly completed the purchase order');
  await nav('content');
  if(!(await evaluate(`document.querySelector('.fd-label-preview').textContent.includes('Not published for this preparation') && !document.querySelector('.fd-label-preview').textContent.includes('Rice, chicken')`)))throw Error('Missing food label inherited another preparation');
  await click('Review label');
  if(await evaluate(`document.querySelector('[aria-label="Authored ingredients"]').value`)!=='')throw Error('Missing label was prefilled from default');
  await input('[aria-label="Authored ingredients"]','Reviewed beef preparation ingredients.');await input('[aria-label="Authored allergens"]','Reviewed allergen statement for the beef preparation.');await input('[aria-label="Authored heating instructions"]','Reviewed heating text for this preparation.');await aria('Confirm exact food label');await click('Publish reviewed label');
  if(!(await evaluate(`document.querySelector('.fd-label-preview').textContent.includes('Reviewed beef preparation ingredients.')`)))throw Error('Exact authored label did not publish');
  await click('Customer choices');await click('Review default');await aria('Confirm food default change');await click('Apply default change');await click('Food labels');
  if(!(await evaluate(`document.querySelector('.fd-label-preview').textContent.includes('Not published for this preparation')`)))throw Error('Changing the recommended default did not flag labels for review');
  await nav('box-plans');await click('7');
  if(!(await evaluate(`document.querySelector('.fd-box-preview').textContent.includes('£49.00') && document.querySelector('.fd-box-preview').textContent.includes('£5.00 to grow to 8')`)))throw Error('Box price uses a flat marginal charge across presets');
  await click('Edit prices');await input('[aria-label="8-dish preset (£)"]','55');await click('Save box prices');await click('8');
  if(!(await evaluate(`document.querySelector('.fd-box-price').textContent==='£55.00'`)))throw Error('Box price edit did not update the customer preview');
  await nav('delivery');await select('Preview order time','after');
  if(!(await evaluate(`document.querySelector('.fd-delivery-preview').textContent.includes('Friday, 13 November')`)))throw Error('Post-cutoff delivery did not roll forward');
  await aria('Delivery schedule configured');
  if(!(await evaluate(`document.querySelector('.fd-delivery-preview h3').textContent==='No delivery date available'`)))throw Error('Unconfigured delivery guessed a date');
  await nav('margin');
  if(!(await evaluate(`document.querySelector('.fd-metrics').textContent.includes('£990.00') && document.querySelector('[data-food-row="prawns"]').textContent.includes('Cost needed')`)))throw Error('Unknown COGS was treated as zero');
  await click('Add missing cost');await input('[aria-label="Missing ingredient cost"]','24');await click('Save cost');
  if(!(await evaluate(`document.querySelector('.fd-metrics').textContent.includes('£1,086.00') && document.querySelector('.fd-metrics').textContent.includes('£694.00')`)))throw Error('Costed report did not recalculate after a real cost');
  await nav('packing');await evaluate(`[...document.querySelectorAll('.fd-pack-line input')].forEach(input=>input.click());true`);await click('Review dispatch');await aria('Confirm dispatch checks');await click('Mark ready');
  if(!(await evaluate(`document.body.innerText.includes('ORD-2048 is ready for dispatch')`)))throw Error('Reviewed packing did not mark the order ready');
  await evaluate(`[...document.querySelectorAll('.fd-packing-order')].find(button=>button.textContent.includes('Alex Morgan')).click();true`);
  if(!(await evaluate(`[...document.querySelectorAll('.fd-pack-line input')].every(input=>input.disabled) && document.querySelector('.fd-pack-check').textContent.includes('Waiting on payment')`)))throw Error('Unpaid order can be dispatched');
  await nav('inventory');await click('Adjust stock');await input('[aria-label="Finished stock count"]','5');await input('[aria-label="Stock count reason"]','Counted at close');
  if(!(await evaluate(`[...document.querySelectorAll('.fd-dialog button')].find(button=>button.textContent.trim()==='Record count').disabled`)))throw Error('Stock adjustment can undermine reservations');
  await input('[aria-label="Finished stock count"]','31');await click('Record count');
  if(!(await evaluate(`document.querySelector('[data-food-row="jollof"]').textContent.includes('31 portions')`)))throw Error('Stock count did not update');
  await page('Emulation.setDeviceMetricsOverride',{width:1600,height:1280,deviceScaleFactor:2,mobile:false});
  await page('Page.navigate',{url:origin+'/food-business.html'});
  await wait(`document.querySelectorAll('[data-dc-slot]').length===41`,'All food artboards did not register');
  const canvasCaptures=['today','plan','batches','ingredients','products','content','margin','customer','order-detail','invoice','sales'];
  for(const scene of scenes){await evaluate(`document.querySelector('[data-dc-slot="${scene.legacy}"] .dc-labeltext').click();true`);await wait(`document.querySelectorAll('[data-food-scene]').length===42`,'Food artboard focus failed: '+scene.id);if(canvasCaptures.includes(scene.id))await capture('food-artboard-'+scene.id);await evaluate(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));true`);}
  await page('Page.navigate',{url:origin+'/food-business.html?scene=invoice'});await wait(`!!document.querySelector('.dc-focus-overlay [data-food-scene=invoice]') || document.querySelectorAll('[data-food-scene=invoice]').length===2`,'Invoice artboard deep link failed');
  await page('Page.navigate',{url:origin+'/index.html'});await wait(`document.querySelectorAll('[data-food-scene]').length===41`,'Main food artboard replacement failed');
  await evaluate(`document.querySelector('[data-dc-slot="cm-prod-orders"] .dc-labeltext').click();true`);await wait(`document.querySelectorAll('[data-food-scene]').length===42`,'Retained legacy artboard focus failed');
  if(apiCalls)throw Error('Food prototypes called application APIs');if(errors.length)throw Error(errors.join('\n'));
  await writeFile(path.join(output,'food-business-design-report.json'),JSON.stringify({checks:['shared seven-tab customer detail structure','scoped profile/addresses/consents, notes and audit events','customer finance allocation and cross-customer isolation','commerce and insight source references','document preview customer scope','customer tab keyboard navigation','connected customer/order/invoice/payment/ledger journey','unpaid orders cannot pack and do not fabricate payments','record search and notification deep links','sales, receipts and balances reconcile with row filters and export','owner and kitchen role navigation previews','41 editable artboards preserve all 24 legacy food surfaces and connect customer, order, invoice and payment journeys','eight primary navigation areas','one primary action per page','82 clean theme captures and 22 focused canvas captures','blocked release has no stock effect','reserved stock netting and pack-rounded buying','foreign-currency supplier guard','Draft/Pending/Complete purchase lifecycle','receipt quantity guard and single application','partial receipts preserve Pending status','saved batch consumption and no automatic restock on cancel','completion actuals and explicit yield without second ingredient consumption','recipe edits leave kitchen snapshots unchanged','batch list and new planned snapshot drafts','exact authored food labels and review consequences','preset box prices and marginal growth','delivery cutoff and absent-promise state','unknown COGS excluded from margin','payment and packing eligibility','finished-stock reservations preserved','all original artboard IDs retained','no horizontal overflow, API calls or runtime exceptions'],scenes:scenes.map(scene=>scene.id)},null,2)+'\n');
  console.log('Food Business checks passed: 41 editable screens, 106 light/dark exports, eight navigation areas, no API calls.');
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
