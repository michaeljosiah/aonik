import { useState } from 'react';
import { Card as AonikCard } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { formatDateTime } from '@/lib/format';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { OrderFulfilmentDto } from '@/types/commerce';

const NEXT_STAGE: Record<string, string> = {
  Confirmed: 'Cooking', Cooking: 'OutForDelivery', OutForDelivery: 'Delivered',
};
export const fulfilmentLabel = (stage: string) => stage === 'OutForDelivery' ? 'Out for delivery' : stage;

export function OrderFulfilmentDetails({ orderId, fulfilment, canWrite, onUpdated }: {
  orderId: string; fulfilment: OrderFulfilmentDto; canWrite: boolean; onUpdated: () => Promise<void>;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const next = NEXT_STAGE[fulfilment.status];
  const advance = async () => {
    if (!next || saving) return;
    setSaving(true);
    setError(null);
    try {
      await commerceStorefrontService.updateOrderFulfilment(orderId, next, fulfilment.version);
      await onUpdated();
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err ? String(err.userMessage ?? '') : '';
      setError(message || 'Fulfilment could not be updated. Reload the order and try again.');
    } finally { setSaving(false); }
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
