import { useEffect, useState } from 'react';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { receiptEmailService } from '@/services/receiptEmailService';
import type { ReceiptEmailHistory as History, ReceiptEmailHistoryItem } from '@/services/receiptEmailService';
import { receiptDeliveryState } from './lib/receiptDelivery';

const statuses = {
  Accepted: {label:'Submitted',description:'Accepted for email delivery. Inbox delivery is not confirmed.',variant:'secondary'},
  Failed: {label:'Not sent',description:'The email was rejected or email sending was not configured.',variant:'destructive'},
  Unknown: {label:'Unconfirmed',description:'Provider acceptance could not be confirmed. Check delivery logs before another send.',variant:'outline'},
  Sending: {label:'Pending',description:'No final send result has been recorded.',variant:'outline'},
} as const;

function Timestamp({value}: {value: string | null}) {
  return value ? <time dateTime={value}>{new Intl.DateTimeFormat(undefined,{dateStyle:'medium',timeStyle:'short',timeZone:'UTC'}).format(new Date(value))} UTC</time> : <span className="text-muted-foreground">Awaiting result</span>;
}

type HistoryActions = {onResend?: (item: ReceiptEmailHistoryItem)=>void; onReviewEmail?: (item: ReceiptEmailHistoryItem)=>void};

export function ReceiptEmailHistoryTable({items,onResend,onReviewEmail}: {items: ReceiptEmailHistoryItem[]} & HistoryActions) {
  return <Table aria-label="Receipt email history">
    <TableHeader><TableRow><TableHead>Requested</TableHead><TableHead>Recipient</TableHead><TableHead>Send status</TableHead><TableHead>Delivery</TableHead>{(onResend || onReviewEmail) && <TableHead>Actions</TableHead>}</TableRow></TableHeader>
    <TableBody>{items.map(item=>{
      const status=statuses[item.status] ?? statuses.Unknown;
      const delivery=receiptDeliveryState(item.deliveryStatus);
      return <TableRow key={item.requestId}>
        <TableCell className="whitespace-nowrap align-top"><Timestamp value={item.requestedAt} /></TableCell>
        <TableCell className="max-w-xs whitespace-normal align-top"><p className="font-medium">{item.maskedRecipient}</p><p className="mt-1 break-all text-xs text-muted-foreground">{item.fileName}</p><p className="mt-1 break-all text-xs text-muted-foreground">Send reference: {item.requestId}</p></TableCell>
        <TableCell className="max-w-xs whitespace-normal align-top"><Badge variant={status.variant}>{status.label}</Badge><p className="mt-2 text-xs text-muted-foreground">{status.description}</p><p className="mt-2 text-xs text-muted-foreground">Send result: <Timestamp value={item.statusUpdatedAt} /></p></TableCell>
        <TableCell className="max-w-xs whitespace-normal align-top"><Badge variant={delivery.variant}><AonikTemplateIcon name={`menu-${delivery.icon}`} size={14}/>{delivery.label}</Badge><p className="mt-2 text-xs text-muted-foreground">{delivery.description}</p>{item.deliveryUpdatedAt && <p className="mt-2 text-xs text-muted-foreground">Reported: <Timestamp value={item.deliveryUpdatedAt}/></p>}</TableCell>
        {(onResend || onReviewEmail) && <TableCell className="whitespace-normal align-top"><div className="flex flex-col items-start gap-2">{onReviewEmail && item.deliveryStatus==='Bounced' && <Button variant="outline" size="sm" onClick={()=>onReviewEmail(item)} aria-label={`Review customer email for request ${item.requestId}`}><AonikTemplateIcon name="menu-email-bounced" size={16}/>Review email</Button>}{onResend && <Button variant="outline" size="sm" disabled={item.status!=='Accepted' && item.status!=='Failed'} onClick={()=>onResend(item)} aria-label={`Resend receipt for request ${item.requestId}`}><AonikTemplateIcon name="menu-communication" size={16} />Resend receipt</Button>}</div>{item.status!=='Accepted' && item.status!=='Failed' && <p className="mt-2 max-w-40 text-xs text-muted-foreground">Resolve this send before sending another copy.</p>}</TableCell>}
      </TableRow>;
    })}</TableBody>
  </Table>;
}

export function ReceiptEmailHistory({orderId,tenantId,onResend,onReviewEmail}: {orderId: string; tenantId: string} & HistoryActions) {
  const [page,setPage]=useState(1);
  const [refresh,setRefresh]=useState(0);
  return <ReceiptEmailHistoryPage key={`${orderId}:${tenantId}:${page}:${refresh}`} orderId={orderId} tenantId={tenantId} page={page} onPage={setPage} onRefresh={()=>setRefresh(value=>value+1)} onResend={onResend} onReviewEmail={onReviewEmail} />;
}

function ReceiptEmailHistoryPage({orderId,tenantId,page,onPage,onRefresh,onResend,onReviewEmail}: {orderId: string; tenantId: string; page: number; onPage: (page: number)=>void; onRefresh: ()=>void} & HistoryActions) {
  const [history,setHistory]=useState<History | null>(null);
  const [error,setError]=useState('');
  const loading=!history && !error;

  useEffect(()=>{
    const controller=new AbortController();
    receiptEmailService.history(orderId,page,{headers:{'X-Tenant-Id':tenantId},signal:controller.signal})
      .then(value=>{if(!controller.signal.aborted) setHistory(value);})
      .catch(error=>{if(!controller.signal.aborted) setError(error && typeof error==='object' && 'userMessage' in error ? String(error.userMessage || 'Unable to load receipt email history.') : 'Unable to load receipt email history. Please try again.');});
    return ()=>controller.abort();
  },[orderId,tenantId,page]);

  return <Card>
    <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-4"><div><CardTitle className="flex items-center gap-2"><AonikTemplateIcon name="menu-communication" size={18} />Receipt email history</CardTitle><CardDescription className="mt-2">Recorded send requests, newest first. Recipients remain masked as recorded at send time.</CardDescription></div><Button variant="outline" size="sm" disabled={loading} onClick={onRefresh}><AonikTemplateIcon name="menu-transactions" size={16} />Refresh history</Button></CardHeader>
    <CardContent>
      {loading && <p role="status" className="text-sm text-muted-foreground">Loading receipt email history…</p>}
      {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
      {history && (history.items.length===0 ? <div className="rounded-lg border border-dashed p-6 text-center"><p className="text-sm font-medium">No receipt emails yet</p><p className="mt-2 text-sm text-muted-foreground">Receipt email requests will appear here after an operator submits them.</p></div> : <ReceiptEmailHistoryTable items={history.items} onResend={onResend} onReviewEmail={onReviewEmail} />)}
      {history && history.totalCount>0 && <div className="mt-4 flex flex-wrap items-center justify-between gap-3"><p className="text-sm text-muted-foreground">Page {history.pageNumber} of {Math.ceil(history.totalCount/history.pageSize)} · {history.totalCount} requests</p><div className="flex gap-2"><Button variant="outline" size="sm" disabled={page<=1} onClick={()=>onPage(page-1)}>Previous</Button><Button variant="outline" size="sm" disabled={page*history.pageSize>=history.totalCount} onClick={()=>onPage(page+1)}>Next</Button></div></div>}
    </CardContent>
  </Card>;
}
