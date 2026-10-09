import { useEffect, useRef, useState } from 'react';
import { Card as AonikCard } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { formatDateTime } from '@/lib/format';
import { getSelectedTenant } from '@/lib/tenantContext';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { OrderFulfilmentDto } from '@/types/commerce';
import { fulfilmentLabel } from '../lib/orderLifecycle';

const NEXT_STAGE: Record<string, string> = {
  Confirmed: 'Cooking', Cooking: 'OutForDelivery', OutForDelivery: 'Delivered',
};

export function OrderFulfilmentDetails({ orderId, tenantId, fulfilment, canWrite, onUpdated }: {
  orderId: string; tenantId: string; fulfilment: OrderFulfilmentDto; canWrite: boolean; onUpdated: () => Promise<void>;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const requestAbort = useRef<AbortController | null>(null);
  useEffect(() => () => requestAbort.current?.abort(), []);
  const next = NEXT_STAGE[fulfilment.status];
  const advance = async () => {
    if (!next || saving || !canWrite || getSelectedTenant()?.tenantId !== tenantId) return;
    const controller = new AbortController();
    requestAbort.current = controller;
    setSaving(true);
    setError(null);
    try {
      await commerceStorefrontService.updateOrderFulfilment(orderId, next, fulfilment.version, { headers: { 'X-Tenant-Id': tenantId }, signal: controller.signal });
      if (controller.signal.aborted || getSelectedTenant()?.tenantId !== tenantId) return;
      await onUpdated();
    } catch (err: unknown) {
      if (controller.signal.aborted || getSelectedTenant()?.tenantId !== tenantId) return;
      const message = err && typeof err === 'object' && 'userMessage' in err ? String(err.userMessage ?? '') : '';
      setError(message || 'Fulfilment could not be updated. Reload the order and try again.');
    } finally { if (!controller.signal.aborted && getSelectedTenant()?.tenantId === tenantId) setSaving(false); }
  };
  return <AonikCard title="Delivery progress" padding={12}>
    <p className="text-sm font-medium">{fulfilmentLabel(fulfilment.status)}</p>
    {fulfilment.history.length > 0 ? <ol className="mt-2 space-y-2 text-xs text-muted-foreground">
      {fulfilment.history.map((event) => <li key={event.toStatus}>
        {fulfilmentLabel(event.toStatus)} · {formatDateTime(event.occurredAtUtc)}
        <span className="block break-all">Staff: {event.actorId}</span>
      </li>)}
    </ol> : <p className="mt-2 text-xs text-muted-foreground">No staff progress updates recorded.</p>}
    {error && <p role="alert" className="mt-2 text-sm text-destructive">{error}</p>}
    {canWrite && next && <Button className="mt-3" size="sm" disabled={saving} onClick={() => void advance()}>
      {saving ? 'Saving…' : `Mark as ${fulfilmentLabel(next)}`}
    </Button>}
  </AonikCard>;
}
