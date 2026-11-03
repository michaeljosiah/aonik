import { useEffect, useRef, useState } from 'react';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { remittanceService } from '@/services/remittanceService';
import type { TransferOrder } from '@/services/remittanceService';

interface ReceiptWorkspace { tenantId: string; name?: string }

export function TransferReceiptButton({order,workspace,onRefresh}: {order: TransferOrder; workspace: ReceiptWorkspace; onRefresh: (order: TransferOrder)=>void}) {
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const controller=useRef<AbortController | null>(null);
  useEffect(()=>()=>controller.current?.abort(),[order.orderId,workspace.tenantId]);

  async function download() {
    if (controller.current && !controller.current.signal.aborted) return;
    const request=new AbortController();controller.current=request;setBusy(true);setError('');
    try {
      // Re-authorize and refresh on every download; a previously loaded screen is not an export cache.
      const latest=await remittanceService.get(order.orderId,{headers:{'X-Tenant-Id':workspace.tenantId},signal:request.signal});
      if (request.signal.aborted) return;
      if (latest.orderId!==order.orderId) throw new Error('The receipt does not match this transfer. Refresh and try again.');
      const file=await remittanceService.receipt(order.orderId,{headers:{'X-Tenant-Id':workspace.tenantId},signal:request.signal});
      if (request.signal.aborted) return;
      const url=URL.createObjectURL(file);
      const anchor=document.createElement('a');anchor.href=url;anchor.download=`transfer-receipt-${(latest.orderNumber || latest.orderId).replace(/[^a-z0-9_-]/gi,'-').slice(0,100)}.pdf`;
      document.body.appendChild(anchor);anchor.click();anchor.remove();
      window.setTimeout(()=>URL.revokeObjectURL(url),1000);
      onRefresh(latest);
    } catch(error) {
      if (!request.signal.aborted) setError(error && typeof error==='object' && 'userMessage' in error ? String(error.userMessage || 'Unable to download the receipt. Please try again.') : error instanceof Error ? error.message : 'Unable to download the receipt. Please try again.');
    } finally {
      if (!request.signal.aborted) {controller.current=null;setBusy(false);}
    }
  }
  return <div className="flex max-w-md flex-col items-end gap-2">
    <Button disabled={busy} onClick={()=>void download()}><AonikTemplateIcon name="menu-invoices" size={18} />{busy ? 'Preparing receipt…' : 'Download receipt'}</Button>
    {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
  </div>;
}
