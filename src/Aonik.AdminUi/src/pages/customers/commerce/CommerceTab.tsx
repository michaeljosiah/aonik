// Commerce tab (Spec 081 §3) — Spec 072's storefront identity made visible on the ONE
// customer record. Sourced from the party-scoped admin read shipped with Spec 073
// (GET /commerce/admin/parties/{partyId}/storefront), which reuses the customer's own
// queries under an admin policy.
//
// v1 renders RECORDED FACTS ONLY. `adopted` is a real stored fact — a party-bound cart whose
// guest token was retired — so it shows as a chip. The built → registered → adopted timeline
// is deliberately absent: AdoptAsync persists no adoption event, UpdatedAt is not an adoption
// timestamp, and registration time is not on the cart, so a timeline would be invented.

import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';

import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import type { AdminPartyStorefrontDto } from '@/types/commerce';

import { formatCurrency } from '../lib/format';
import { BoxHistoryCard } from './BoxHistoryCard';

interface CommerceTabProps {
  partyId: string;
}

export function CommerceTab({ partyId }: CommerceTabProps) {
  const [data, setData] = useState<AdminPartyStorefrontDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);

  const load = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const result = await commerceStorefrontService.getPartyStorefront(partyId);
      if (requestIdRef.current !== requestId) return;
      setData(result);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load storefront activity.');
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [partyId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return (
      <AonikCard title="Storefront">
        <div className="flex items-center justify-center py-6">
          <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      </AonikCard>
    );
  }

  if (error) {
    return (
      <AonikCard title="Storefront">
        <div className="rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          {error}
        </div>
        <button
          type="button"
          onClick={() => void load()}
          className="mt-3 text-xs text-[var(--color-brand-primary)] hover:underline"
        >
          Retry
        </button>
      </AonikCard>
    );
  }

  const orders = data?.orders ?? [];
  const activeCart = data?.activeCart ?? null;

  // Storefront value is summed from the SAME rows the box history renders, so the figure and
  // the table can never disagree. Mixed currencies are listed separately — adding them would
  // invent an exchange rate.
  const valueByCurrency = new Map<string, number>();
  for (const order of orders) {
    valueByCurrency.set(order.currency, (valueByCurrency.get(order.currency) ?? 0) + order.total);
  }
  const totals = [...valueByCurrency.entries()].sort((a, b) => b[1] - a[1]);

  return (
    <div className="flex flex-col gap-4">
      <AonikCard title="Storefront profile" subtitle="Derived from this party's own box orders">
        <div className="flex flex-wrap items-start gap-x-10 gap-y-4">
          <div>
            <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
              Boxes ordered
            </div>
            <div className="mt-1 font-[family-name:var(--font-mono)] text-lg font-semibold tabular-nums text-[var(--color-text-primary)]">
              {orders.length.toLocaleString()}
            </div>
          </div>

          <div>
            <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
              Storefront value
            </div>
            <div className="mt-1 flex flex-col gap-0.5">
              {totals.length === 0 ? (
                <span className="text-sm text-[var(--color-text-tertiary)]">—</span>
              ) : (
                totals.map(([currency, amount]) => (
                  <span
                    key={currency}
                    className="font-[family-name:var(--font-mono)] text-lg font-semibold tabular-nums text-[var(--color-text-primary)]"
                  >
                    {formatCurrency(amount, currency)}
                  </span>
                ))
              )}
            </div>
          </div>

          {activeCart && (
            <div>
              <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Active cart
              </div>
              <div className="mt-1 font-[family-name:var(--font-mono)] text-lg font-semibold tabular-nums text-[var(--color-text-primary)]">
                {activeCart.filled}/{activeCart.size}
              </div>
            </div>
          )}

          {data?.adopted && (
            <div>
              <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Identity
              </div>
              <div className="mt-1.5">
                {/* A recorded fact, not a timeline: the guest token on a party-bound cart
                    was retired. No timestamps — nothing persists when adoption happened. */}
                <Pill tone="info" size="sm">
                  Guest-built cart adopted — token retired
                </Pill>
              </div>
            </div>
          )}
        </div>
      </AonikCard>

      <BoxHistoryCard orders={orders} />
    </div>
  );
}
