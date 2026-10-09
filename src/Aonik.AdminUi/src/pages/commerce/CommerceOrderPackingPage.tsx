import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link, useParams } from 'react-router-dom';

import { Card, PageHeader } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminOrderPackingDto } from '@/types/commerce';

import { PackingSlip } from './components/PackingSlip';
import './packing.css';

export function CommerceOrderPackingPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const [packing, setPacking] = useState<AdminOrderPackingDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setPacking(null);
    setError(null);
    setLoading(true);
    if (!orderId) return () => controller.abort();
    void commerceStorefrontService.getOrderPacking(orderId, controller.signal)
      .then(result => { if (!controller.signal.aborted) setPacking(result); })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message = err && typeof err === 'object' && 'userMessage' in err ? String(err.userMessage ?? '') : '';
        setError(message || 'This packing slip could not be loaded. Only confirmed paid orders can be printed.');
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [orderId, attempt]);

  const current = packing?.orderId === orderId ? packing : null;
  return <div className="flex flex-col gap-5 p-6 md:px-8">
    <PageHeader title="Packing slip" subtitle="Recipient copy of the confirmed order" />
    <div className="flex gap-3">
      <Button variant="outline" asChild><Link to={`/commerce/orders/${orderId}`}>Back to order</Link></Button>
      <Button disabled={!current || loading} onClick={() => window.print()}>Print packing slip</Button>
    </div>
    {loading ? <p>Loading packing slip…</p> : current ? <Card padding={20}><PackingSlip packing={current} /></Card> : <div className="space-y-3">
      <p role="alert">{error ?? 'This packing slip could not be loaded.'}</p>
      <Button variant="outline" onClick={() => setAttempt(value => value + 1)}>Try again</Button>
    </div>}
    {current && !loading && createPortal(<div className="packing-print-root"><PackingSlip packing={current} /></div>, document.body)}
  </div>;
}
