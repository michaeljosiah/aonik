import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { receiptEmailService } from '@/services/receiptEmailService';
import type { ReceiptEmailHistoryItem, ReceiptRecipientReview } from '@/services/receiptEmailService';

const message=(error: unknown)=>error && typeof error==='object' && 'userMessage' in error ? String(error.userMessage || 'Unable to review the saved email.') : error instanceof Error ? error.message : 'Unable to review the saved email.';

export function ReviewReceiptEmailDialog({orderId,tenantId,item,onClose,onContinue}: {
  orderId: string; tenantId: string; item: ReceiptEmailHistoryItem; onClose: ()=>void; onContinue: ()=>void;
}) {
  const [review,setReview]=useState<ReceiptRecipientReview | null>(null);
  const [email,setEmail]=useState('');
  const [confirmed,setConfirmed]=useState(false);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  const [retry,setRetry]=useState(0);
  const lock=useRef(false);
  const mounted=useRef(true);
  const changed=!!review && email.trim().toLowerCase()!==review.contact.email.toLowerCase();

  useEffect(()=>{
    mounted.current=true;
    const controller=new AbortController();
    setReview(null);setError('');setConfirmed(false);
    receiptEmailService.reviewRecipient(orderId,item.requestId,{headers:{'X-Tenant-Id':tenantId},signal:controller.signal})
      .then(value=>{if(!controller.signal.aborted) {setReview(value);setEmail(value.contact.email);}})
      .catch(error=>{if(!controller.signal.aborted) setError(message(error));});
    return ()=>{mounted.current=false;controller.abort();};
  },[orderId,tenantId,item.requestId,retry]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if(lock.current || !review || !confirmed || !event.currentTarget.checkValidity()) return;
    lock.current=true;setBusy(true);setError('');
    try {
      if(changed) await receiptEmailService.updateRecipient(orderId,item.requestId,{expectedEmail:review.contact.email,newEmail:email.trim(),confirmUpdate:true},{headers:{'X-Tenant-Id':tenantId}});
      if(mounted.current) onContinue();
    } catch(error) {if(mounted.current) {setError(message(error));setConfirmed(false);}}
    finally {lock.current=false;if(mounted.current) setBusy(false);}
  }

  return <Dialog open onOpenChange={open=>{if(!open && !busy) onClose();}}><DialogContent className="max-h-[90dvh] overflow-y-auto">
    <DialogHeader><DialogTitle className="flex items-center gap-2"><AonikTemplateIcon name="menu-email-bounced" size={20}/>Review customer email</DialogTitle><DialogDescription>Review the saved contact used for this bounced receipt. Save any correction, then confirm the recipient before sending another copy.</DialogDescription></DialogHeader>
    <div className="rounded-lg border p-3 text-sm"><p className="font-medium">Bounced recipient: {item.maskedRecipient}</p><p className="mt-1 break-all text-xs text-muted-foreground">Send reference: {item.requestId}</p></div>
    {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
    {!review && !error && <p role="status" className="text-sm text-muted-foreground">Loading the saved customer email…</p>}
    {!review && error && <Button variant="outline" onClick={()=>setRetry(value=>value+1)}>Reload saved email</Button>}
    {review && <form onSubmit={event=>void save(event)} className="min-w-0 space-y-4">
      <p className="text-sm font-medium">{review.customerName} · {review.orderNumber}</p>
      {review.changedSinceSend && <Alert><AlertDescription>This saved email has changed since the bounced receipt was sent. Review the current address below.</AlertDescription></Alert>}
      <Field><FieldLabel htmlFor="receipt-review-email">Saved customer email</FieldLabel><Input id="receipt-review-email" type="email" autoComplete="off" required maxLength={320} value={email} disabled={busy} onChange={event=>{setEmail(event.target.value);setConfirmed(false);}}/><FieldDescription>Changes update this customer’s saved contact for future communications. {review.contact.isPrimary ? 'This remains the primary email.' : 'The primary email is unchanged.'}</FieldDescription></Field>
      <div className="flex items-start gap-2"><Checkbox id="receipt-review-confirm" checked={confirmed} disabled={busy} onCheckedChange={value=>setConfirmed(value===true)}/><label htmlFor="receipt-review-confirm" className="text-sm leading-relaxed">I have verified this email address with the customer{changed ? ' and confirm updating their saved contact' : ''}.</label></div>
      <p className="text-xs text-muted-foreground">Saving does not send a receipt. The next step lets you select and confirm the current saved recipient.</p>
      <DialogFooter className="flex-wrap gap-2"><Button type="button" variant="outline" disabled={busy} onClick={()=>setRetry(value=>value+1)}>Reload saved email</Button><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Cancel</Button><Button type="submit" disabled={busy || !confirmed || !email.trim()}><AonikTemplateIcon name="menu-customers" size={16}/>{busy ? 'Saving…' : changed ? 'Save email & review receipt' : 'Continue to resend'}</Button></DialogFooter>
    </form>}
    {!review && <DialogFooter><Button variant="outline" onClick={onClose}>Close</Button></DialogFooter>}
  </DialogContent></Dialog>;
}
