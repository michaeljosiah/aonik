// Box history (Spec 081 §3) — the party's checked-out storefront orders, from the
// party-scoped admin read. Exactly what the customer sees in their own account.

import { Card as AonikCard, Pill, type PillTone } from '@/components/layout/aonik';
import type { StorefrontOrderSummaryDto } from '@/types/commerce';

import { formatCurrency, formatDate } from '../lib/format';

const STATUS_TONE: Record<string, PillTone> = {
  Complete: 'success',
  Pending: 'warning',
  PendingFunding: 'warning',
  Cancelled: 'muted',
  Failed: 'danger',
  Expired: 'muted',
};

interface BoxHistoryCardProps {
  orders: StorefrontOrderSummaryDto[];
}

export function BoxHistoryCard({ orders }: BoxHistoryCardProps) {
  return (
    <AonikCard
      title="Box history"
      subtitle="Party-scoped — exactly what the customer sees in their own account"
    >
      {orders.length === 0 ? (
        <p className="py-4 text-center text-sm text-[var(--color-text-secondary)]">
          No storefront orders yet.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-[var(--color-border-light)] text-left text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                <th className="px-2 py-2.5">Order</th>
                <th className="px-2 py-2.5">Date</th>
                <th className="px-2 py-2.5">Size</th>
                <th className="px-2 py-2.5">Status</th>
                <th className="px-2 py-2.5 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order, idx) => (
                <tr
                  key={order.orderId}
                  className={
                    idx === orders.length - 1
                      ? ''
                      : 'border-b border-[var(--color-border-light)]'
                  }
                >
                  <td className="px-2 py-2.5">
                    <span className="font-[family-name:var(--font-mono)] text-[11px] font-medium text-[var(--color-text-primary)]">
                      ORD-{order.orderId.replace(/-/g, '').slice(0, 8).toUpperCase()}
                    </span>
                  </td>
                  <td className="px-2 py-2.5">
                    <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                      {formatDate(order.placedAtUtc)}
                    </span>
                  </td>
                  <td className="px-2 py-2.5">
                    {/* Extras summary needs per-order line detail the party-scoped summary
                        does not carry, so the column shows size alone rather than an
                        approximation. */}
                    <span className="text-[12.5px] text-[var(--color-text-secondary)]">
                      {order.boxSize != null ? `${order.boxSize}` : '—'}
                    </span>
                  </td>
                  <td className="px-2 py-2.5">
                    <Pill tone={STATUS_TONE[order.status] ?? 'default'} dot size="sm">
                      {order.status}
                    </Pill>
                  </td>
                  <td className="px-2 py-2.5 text-right">
                    <span className="font-[family-name:var(--font-mono)] text-[12.5px] font-medium text-[var(--color-text-primary)]">
                      {formatCurrency(order.total, order.currency)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </AonikCard>
  );
}
