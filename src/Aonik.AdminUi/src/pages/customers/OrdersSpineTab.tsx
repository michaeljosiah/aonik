// Orders tab — ONE spine (Spec 081 §2). Boxes, bill payments and transfers all live on the
// same Order record (ADR-011), so this lists every type and distinguishes them with a chip
// rather than sending each product line to its own screen.
//
// Scope: orders this party PAYS FOR. The spine's PayerPartyId filter is what ships today, so
// orders where the party is only the receiver are invisible here — stated in the caption
// rather than silently implied. A participant-role filter is a shared follow-up with Spec
// 080's registry counts, which use this same payer-scoped predicate so the two always agree.

import { FileText, RefreshCw } from 'lucide-react';

import { Card as AonikCard, Pill, type PillTone } from '@/components/layout/aonik';
import type { OrderListItem } from '@/types';

import { formatCurrency, formatDate } from './lib/format';
import { presentOrderType } from './lib/orderTypePresentation';

const ORDER_STATUS_TONE: Record<string, PillTone> = {
  Complete: 'success',
  Pending: 'warning',
  PendingFunding: 'warning',
  Cancelled: 'muted',
  Failed: 'danger',
  Expired: 'muted',
};

interface OrdersSpineTabProps {
  orders: OrderListItem[];
  totalCount: number;
  loading: boolean;
  error: string | null;
  onView: (orderId: string) => void;
  onReload: () => void;
}

export function OrdersSpineTab({
  orders,
  totalCount,
  loading,
  error,
  onView,
  onReload,
}: OrdersSpineTabProps) {
  return (
    <AonikCard
      title="Orders"
      subtitle={
        totalCount > 0
          ? `${totalCount.toLocaleString()} total · orders this customer pays for`
          : 'Orders this customer pays for'
      }
      action={
        <button
          type="button"
          onClick={onReload}
          className="text-xs text-[var(--color-brand-primary)] hover:underline"
        >
          Refresh
        </button>
      }
    >
      <p className="mb-3 text-[11px] leading-relaxed text-[var(--color-text-tertiary)]">
        Every order, one spine — boxes, bill payments and transfers share the Order record
        (ADR-011); filter by type, never by screen.
      </p>

      {error && (
        <div className="mb-3 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-6">
          <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      ) : orders.length === 0 ? (
        <div className="py-6 text-center">
          <FileText className="mx-auto mb-2 h-8 w-8 text-[var(--color-text-tertiary)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">
            No orders recorded for this customer yet.
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-[var(--color-border-light)] text-left text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                <th className="px-2 py-2.5">Order</th>
                <th className="px-2 py-2.5">Type</th>
                <th className="px-2 py-2.5">Date</th>
                <th className="px-2 py-2.5">Status</th>
                <th className="px-2 py-2.5 text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order, idx) => {
                const isLast = idx === orders.length - 1;
                const statusTone = ORDER_STATUS_TONE[order.status] ?? 'default';
                const type = presentOrderType(order.orderType);
                return (
                  <tr
                    key={order.orderId}
                    onClick={() => onView(order.orderId)}
                    className={
                      'cursor-pointer transition-colors hover:bg-[var(--color-surface-inset)] ' +
                      (isLast ? '' : 'border-b border-[var(--color-border-light)]')
                    }
                  >
                    <td className="px-2 py-2.5">
                      <span className="font-[family-name:var(--font-mono)] text-[11px] font-medium text-[var(--color-brand-primary)]">
                        ORD-{order.orderId.replace(/-/g, '').slice(0, 8).toUpperCase()}
                      </span>
                    </td>
                    <td className="px-2 py-2.5">
                      <Pill tone={type.tone} size="sm">
                        {type.label}
                      </Pill>
                    </td>
                    <td className="px-2 py-2.5">
                      <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                        {formatDate(order.createdAt)}
                      </span>
                    </td>
                    <td className="px-2 py-2.5">
                      <Pill tone={statusTone} dot size="sm">
                        {order.status}
                      </Pill>
                    </td>
                    <td className="px-2 py-2.5 text-right">
                      <span className="font-[family-name:var(--font-mono)] text-[12.5px] font-medium text-[var(--color-text-primary)]">
                        {formatCurrency(order.totalAmountIn, order.originCurrency)}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </AonikCard>
  );
}
