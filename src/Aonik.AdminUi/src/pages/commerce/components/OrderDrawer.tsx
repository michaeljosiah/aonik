// Storefront order drawer (Spec 083 §2) — route-addressable at /commerce/orders/:orderId so
// deep links (including Spec 084's recent-orders rows) open it directly.

import { useCallback, useEffect, useState } from 'react';

import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import { formatCurrency, formatDate } from '@/lib/format';
import type { AdminOrderStorefrontDto } from '@/types/commerce';

import { BuyerLabel } from './BuyerLabel';
import { LifecycleStepper } from './LifecycleStepper';
import { OrderLineItems } from './OrderLineItems';
import { orderLifecycle } from '../lib/orderLifecycle';
import { paymentTone, fulfilmentTone } from '../lib/statusTone';

interface OrderDrawerProps {
  orderId: string;
  onClose: () => void;
}

export function OrderDrawer({ orderId, onClose }: OrderDrawerProps) {
  const [order, setOrder] = useState<AdminOrderStorefrontDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setOrder(await commerceStorefrontService.getStorefrontOrder(orderId));
    } catch (err: unknown) {
      setOrder(null);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'This order could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [orderId]);

  useEffect(() => {
    void load();
  }, [load]);

  const charge = order?.charge;
  const currency = charge?.currency ?? '';

  return (
    <Sheet open onOpenChange={(open) => !open && onClose()}>
      <SheetContent size="md">
        <SheetHeader
          title="Storefront order"
          subtitle={order ? order.orderId : orderId}
        />

        <SheetBody>
          {loading ? (
            <p className="py-8 text-center text-sm text-[var(--color-text-secondary)]">Loading…</p>
          ) : !order ? (
            <div className="flex flex-col items-center gap-3 py-10">
              <p className="text-sm text-[var(--color-text-secondary)]">
                {error ?? 'This order could not be loaded.'}
              </p>
              <Button variant="outline" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          ) : (
            <div className="flex flex-col gap-4">
              <AonikCard padding={12}>
                <LifecycleStepper lifecycle={orderLifecycle(order)} />
              </AonikCard>

              <div className="grid grid-cols-2 gap-3">
                <AonikCard title="Payment" padding={12}>
                  <Pill tone={paymentTone(order.paymentStatus)}>{order.paymentStatus}</Pill>
                </AonikCard>
                <AonikCard title="Fulfilment" padding={12}>
                  <Pill tone={fulfilmentTone(order.fulfilmentStatus)}>
                    {order.fulfilmentStatus}
                  </Pill>
                </AonikCard>
              </div>

              <AonikCard title="Buyer" padding={12}>
                <div className="flex items-center justify-between gap-3">
                  <BuyerLabel buyerKind={order.buyerKind} buyerPartyId={order.buyerPartyId} />
                  <span className="text-[11.5px] text-[var(--color-text-tertiary)]">
                    Placed {formatDate(order.placedAtUtc)}
                  </span>
                </div>
              </AonikCard>

              <AonikCard
                title="Items"
                subtitle={
                  order.boxSize != null
                    ? `Box of ${order.boxSize} — each preparation below is what the kitchen makes`
                    : undefined
                }
                padding={12}
              >
                <OrderLineItems
                  items={order.items}
                  selections={order.selections}
                  currency={currency}
                />
              </AonikCard>

              {charge && (
                <AonikCard title="Charge" padding={12}>
                  <dl className="flex flex-col gap-1.5">
                    <ChargeRow label="Subtotal" amount={charge.subtotal} currency={currency} />
                    {charge.discountTotal !== 0 && (
                      <ChargeRow
                        label="Discount"
                        amount={-Math.abs(charge.discountTotal)}
                        currency={currency}
                        chip={charge.discountCode}
                      />
                    )}
                    {charge.taxTotal !== 0 && (
                      <ChargeRow label="Tax" amount={charge.taxTotal} currency={currency} />
                    )}
                    <div className="mt-1 border-t border-[var(--color-border-light)] pt-1.5">
                      <ChargeRow
                        label="Total payable"
                        amount={charge.total}
                        currency={currency}
                        emphasis
                      />
                    </div>
                  </dl>
                </AonikCard>
              )}
            </div>
          )}
        </SheetBody>

        <SheetFooter>
          {/* Refund is a Finance HIGH-tier action (money movement) and is not wired for
              commerce — it stays visibly disabled with the reason rather than absent, so the
              operator learns where the capability lives instead of wondering. */}
          <Button variant="outline" disabled title="Refunds are a Finance action and are not wired for storefront orders yet">
            Refund
          </Button>
          <Button onClick={onClose}>Close</Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function ChargeRow({
  label,
  amount,
  currency,
  chip,
  emphasis,
}: {
  label: string;
  amount: number;
  currency: string;
  chip?: string | null;
  emphasis?: boolean;
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="flex items-center gap-1.5 text-[12.5px] text-[var(--color-text-secondary)]">
        {label}
        {chip && (
          <Pill tone="muted" size="sm">
            {chip}
          </Pill>
        )}
      </dt>
      <dd
        className={`font-[family-name:var(--font-mono)] tabular-nums ${
          emphasis
            ? 'text-[13.5px] font-semibold text-[var(--color-text-primary)]'
            : 'text-[12.5px] text-[var(--color-text-primary)]'
        }`}
      >
        {formatCurrency(amount, currency)}
      </dd>
    </div>
  );
}
