import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';

import { useAuth } from '@/auth/useAuth';
import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { NativeSelect } from '@/components/ui/native-select';
import { useModules } from '@/modules/useModules';
import { receiptEmailService } from '@/services/receiptEmailService';
import type { ReceiptEmailHistoryItem, ReceiptEmailOptions, ReceiptEmailResult } from '@/services/receiptEmailService';
import type { TransferOrder } from '@/services/remittanceService';

import { readPendingReceiptEmail, receiptEmailHash, receiptEmailStorageKey } from './lib/receiptEmail';
import type { PendingReceiptEmail } from './lib/receiptEmail';
import { receiptDeliveryState } from './lib/receiptDelivery';

const message=(error: unknown)=>error && typeof error==='object' && 'userMessage' in error ? String(error.userMessage || 'Unable to send the receipt.') : error instanceof Error ? error.message : 'Unable to send the receipt.';

export function SendReceiptButton({order,tenantId,onUpdated,resendFrom,onResendClose}: {order: TransferOrder; tenantId: string; onUpdated: ()=>void; resendFrom?: ReceiptEmailHistoryItem | null; onResendClose?: ()=>void}) {
  const {user}=useAuth();
  const {hasPermission,allowsPolicy,isPathVisible}=useModules();
  const canSend=hasPermission('Payment.Read') && hasPermission('Customers.Read') && hasPermission('Customers.Write') && allowsPolicy('AdminWritePolicy');
  const key=user?.id ? receiptEmailStorageKey(tenantId,user.id,order.orderId) : null;
  const [open,setOpen]=useState(false);
  const dialogOpen=open || !!resendFrom;
  const [options,setOptions]=useState<ReceiptEmailOptions | null>(null);
  const [contactId,setContactId]=useState('');
  const [confirmed,setConfirmed]=useState(false);
  const [loading,setLoading]=useState(false);
  const [busy,setBusy]=useState(false);
  const [pending,setPending]=useState<PendingReceiptEmail | null>(null);
  const [recoveryError,setRecoveryError]=useState('');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [result,setResult]=useState<ReceiptEmailResult | null>(null);
  const [retryLoad,setRetryLoad]=useState(0);
  const lock=useRef(false);
  const activeScope=useRef(key);activeScope.current=key;
  const contact=options?.contacts.find(contact=>contact.contactId===contactId);

  useEffect(()=>{
    if (!dialogOpen || !key || !canSend) return;
    const controller=new AbortController();
    setLoading(true);setError('');setOptions(null);setConfirmed(false);setResult(null);setRecoveryError('');
    let previous: PendingReceiptEmail | null=null;
    try {previous=readPendingReceiptEmail(sessionStorage,key);setPending(previous);}
    catch(error) {setRecoveryError(message(error));}
    receiptEmailService.options(order.orderId,{headers:{'X-Tenant-Id':tenantId},signal:controller.signal})
      .then(value=>{
        if (controller.signal.aborted) return;
        setOptions(value);setContactId(previous?.contactId || (resendFrom ? '' : value.contacts.find(contact=>contact.isPrimary)?.contactId || (value.contacts.length===1 ? value.contacts[0].contactId : '')));
      }).catch(error=>{if (!controller.signal.aborted) setError(message(error));})
      .finally(()=>{if (!controller.signal.aborted) setLoading(false);});
    return ()=>controller.abort();
  },[dialogOpen,key,order.orderId,tenantId,canSend,retryLoad,resendFrom]);

  function close() {setOpen(false);onResendClose?.();}

  function applyResult(value: ReceiptEmailResult,scope: string) {
    if (activeScope.current!==scope) return;
    setResult(value);
    if (value.status==='Accepted' || value.status==='Failed') {
      // Clearing storage is required before another send can be admitted by this tab.
      sessionStorage.removeItem(scope);setPending(null);setConfirmed(false);
      if (value.status==='Accepted') {
        const delivery=receiptDeliveryState(value.deliveryStatus);
        setNotice(value.deliveryStatus==='Delivered' ? `Receipt delivered to the recipient’s mail server for ${value.maskedRecipient}.` :
          ['Bounced','Suppressed','Quarantined','FilteredSpam','Failed'].includes(value.deliveryStatus ?? '') ? `Receipt email was not delivered to ${value.maskedRecipient} (${delivery.label}). Review the saved address and delivery history before another copy.` : `Receipt submitted for email delivery to ${value.maskedRecipient}.`);
        close();
      }
      else setError('The receipt was not sent. Check the email configuration or provider rejection, then try again.');
    }
  }

  async function send() {
    if (lock.current || !canSend || !key || !contact || !confirmed || recoveryError || (resendFrom && pending)) return;
    const scope=key;lock.current=true;setBusy(true);setError('');
    try {
      const emailHash=await receiptEmailHash(contact.email);
      // Keep the same reference even if validation rejected an outdated contact before a send
      // was reserved. The server binds a reserved reference to its original recipient and
      // rejects substitution, so reviewing a changed contact cannot dispatch a second email.
      const attempt={requestId:pending?.requestId || crypto.randomUUID(),contactId:contact.contactId,emailHash};
      sessionStorage.setItem(scope,JSON.stringify(attempt));setPending(attempt);
      const response=await receiptEmailService.send(order.orderId,{requestId:attempt.requestId,contactId:attempt.contactId,expectedEmail:contact.email,confirmSend:true},{headers:{'X-Tenant-Id':tenantId}});
      applyResult(response,scope);
    } catch(error) {if (activeScope.current===scope) setError(message(error));}
    finally {lock.current=false;if (activeScope.current===scope) {setBusy(false);onUpdated();}}
  }

  async function checkStatus() {
    if (lock.current || !pending || !key || !canSend) return;
    const scope=key;lock.current=true;setBusy(true);setError('');
    try {applyResult(await receiptEmailService.status(order.orderId,pending.requestId,{headers:{'X-Tenant-Id':tenantId}}),scope);}
    catch(error) {if (activeScope.current===scope) setError(`${message(error)} The original send reference is retained; retrying that request will not start a second email.`);}
    finally {lock.current=false;if (activeScope.current===scope) {setBusy(false);onUpdated();}}
  }

  if (!canSend) return null;
  return <div className="flex max-w-md flex-col items-end gap-2">
    <Button variant="outline" onClick={()=>{setNotice('');onResendClose?.();setOpen(true);}}><AonikTemplateIcon name="menu-communication" size={18} />Send receipt</Button>
    {notice && <Alert role="status" variant={['Bounced','Suppressed','Quarantined','FilteredSpam','Failed'].includes(result?.deliveryStatus ?? '') ? 'destructive' : undefined}><AlertDescription>{notice}</AlertDescription></Alert>}
    <Dialog open={dialogOpen} onOpenChange={value=>{if (!busy) {if(value) setOpen(true);else close();}}}><DialogContent>
      <DialogHeader><DialogTitle>{resendFrom ? 'Resend transfer receipt' : 'Send transfer receipt'}</DialogTitle><DialogDescription>{resendFrom ? 'Send another copy after selecting and confirming a saved customer email address. A fresh PDF uses the current saved transfer status.' : 'Email a PDF receipt to a saved email address for this customer.'}</DialogDescription></DialogHeader>
      {resendFrom && <div className="rounded-lg border p-3 text-sm"><p className="font-medium">Previous recipient: {resendFrom.maskedRecipient}</p><p className="mt-1 break-all text-xs text-muted-foreground">Previous send reference: {resendFrom.requestId}</p><p className="mt-2 text-xs text-muted-foreground">Choose the saved address below; a masked historical address cannot identify the recipient automatically.</p></div>}
      {resendFrom?.deliveryStatus && <p className="text-sm text-muted-foreground">Previous delivery: {receiptDeliveryState(resendFrom.deliveryStatus).label}</p>}
      {(error || recoveryError) && <Alert variant="destructive"><AlertDescription>{recoveryError || error}</AlertDescription></Alert>}
      {loading && <p role="status" className="text-sm text-muted-foreground">Loading customer email addresses…</p>}
      {!loading && !options && <Button variant="outline" onClick={()=>setRetryLoad(value=>value+1)}>Retry loading recipients</Button>}
      {options && <>
        <div className="rounded-lg border bg-muted p-4 text-sm"><p className="font-medium">{options.customerName} · {options.orderNumber}</p><Badge variant="secondary" className="my-2">{options.transferStatus}</Badge><p className="break-all text-muted-foreground">{options.fileName}</p></div>
        {options.contacts.length===0 ? <Alert><AlertDescription>This customer has no valid saved email address. {isPathVisible(`/customers/${order.customerPartyId}`) && <Link className="text-primary underline" to={`/customers/${order.customerPartyId}`}>Update customer contact details</Link>}</AlertDescription></Alert> : <Field><FieldLabel htmlFor="receipt-email-contact">Send to</FieldLabel><NativeSelect id="receipt-email-contact" value={contactId} disabled={busy} onChange={event=>{setContactId(event.target.value);setConfirmed(false);}}><option value="">Choose a saved email address</option>{options.contacts.map(contact=><option key={contact.contactId} value={contact.contactId}>{contact.email}{contact.isPrimary ? ' · Primary' : ''}</option>)}</NativeSelect><FieldDescription>The PDF is generated from the saved transfer. Amounts, fees, masked destination and current status are included.</FieldDescription></Field>}
        {pending && <Alert><AlertDescription>A receipt email request is awaiting a confirmed result. Check its status or retry with the same reference. If a send was already recorded, its recipient cannot be changed.</AlertDescription></Alert>}
        {resendFrom && pending && <Alert><AlertDescription>Resolve the earlier request before sending another copy. You can check its status here or use Send receipt to recover the original request.</AlertDescription></Alert>}
        {result?.status==='Unknown' && <Alert><AlertDescription>Provider acceptance could not be confirmed. Check delivery logs before requesting another copy. Checking status does not resend the email.</AlertDescription></Alert>}
        {contact && <div className="flex items-start gap-2"><Checkbox id="receipt-email-confirm" checked={confirmed} disabled={busy || !!recoveryError} onCheckedChange={value=>setConfirmed(value===true)} /><label htmlFor="receipt-email-confirm" className="text-sm leading-relaxed">I confirm the customer and selected email address, and want to send this receipt.</label></div>}
      </>}
      <DialogFooter><Button variant="outline" disabled={busy} onClick={close}>Close</Button>{pending && <Button variant="outline" disabled={busy || !!recoveryError} onClick={()=>void checkStatus()}>Check send status</Button>}<Button disabled={busy || loading || !contact || !confirmed || !!recoveryError || !!(resendFrom && pending)} onClick={()=>void send()}><AonikTemplateIcon name="menu-communication" size={16} />{busy ? 'Processing…' : resendFrom ? 'Confirm & resend receipt' : pending ? 'Retry same request' : 'Send receipt email'}</Button></DialogFooter>
    </DialogContent></Dialog>
  </div>;
}
