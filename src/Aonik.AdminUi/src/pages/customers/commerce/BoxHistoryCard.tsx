// Box history (Spec 081 §3) — the party's checked-out storefront orders, from the
// party-scoped admin read. Exactly what the customer sees in their own account.

import { Card as AonikCard, Pill, type PillTone } from '@/components/layout/aonik';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { StorefrontOrderSummaryDto } from '@/types/commerce';

import { formatCurrency, formatDate } from '@/lib/format';

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
        <p className="py-4 text-center text-sm text-muted-foreground">
          No storefront orders yet.
        </p>
      ) : (
        <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead numeric className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Order</TableHead>
                <TableHead className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Date</TableHead>
                <TableHead numeric className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Size</TableHead>
                <TableHead className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Status</TableHead>
                <TableHead numeric className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Total</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orders.map((order) => (
                <TableRow key={order.orderId} className="hover:bg-transparent">
                  <TableCell numeric className="px-2 py-2.5">
                    <span className="text-[11px] font-medium text-foreground">
                      ORD-{order.orderId.replace(/-/g, '').slice(0, 8).toUpperCase()}
                    </span>
                  </TableCell>
                  <TableCell className="px-2 py-2.5">
                    <span className="font-mono text-[11px] tabular-nums text-muted-foreground">
                      {formatDate(order.placedAtUtc)}
                    </span>
                  </TableCell>
                  <TableCell numeric className="px-2 py-2.5">
                    {/* Extras summary needs per-order line detail the party-scoped summary
                        does not carry, so the column shows size alone rather than an
                        approximation. */}
                    <span className="text-[12.5px] text-muted-foreground">
                      {order.boxSize != null ? `${order.boxSize}` : '—'}
                    </span>
                  </TableCell>
                  <TableCell className="px-2 py-2.5">
                    <Pill tone={STATUS_TONE[order.status] ?? 'default'} dot>
                      {order.status}
                    </Pill>
                  </TableCell>
                  <TableCell numeric className="px-2 py-2.5">
                    <span className="text-[12.5px] font-medium text-foreground">
                      {formatCurrency(order.total, order.currency)}
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
      )}
    </AonikCard>
  );
}
