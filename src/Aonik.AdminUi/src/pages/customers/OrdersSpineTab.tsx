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
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { OrderListItem } from '@/types';

import { formatCurrency, formatDate } from '@/lib/format';
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
  /** More orders exist beyond what is loaded — the list is a window, not the whole record. */
  hasMore: boolean;
  onLoadMore: () => void;
}

export function OrdersSpineTab({
  orders,
  totalCount,
  loading,
  error,
  onView,
  onReload,
  hasMore,
  onLoadMore,
}: OrdersSpineTabProps) {
  return (
    <AonikCard
      title="Orders"
      subtitle={
        totalCount > 0
          ? `Showing ${orders.length.toLocaleString()} of ${totalCount.toLocaleString()} · orders this customer pays for`
          : 'Orders this customer pays for'
      }
      action={
        <Button type="button" variant="link" size="sm" onClick={onReload} className="h-auto p-0 text-xs">
          Refresh
        </Button>
      }
    >
      <p className="mb-3 text-[11px] leading-relaxed text-muted-foreground">
        Every order, one spine — boxes, bill payments and transfers share the Order record
        (ADR-011); filter by type, never by screen.
      </p>

      {error && (
        <Alert variant="destructive" className="mb-3 px-3 py-2">
          <AlertDescription className="text-xs">{error}</AlertDescription>
        </Alert>
      )}

      {/* Full-page spinner only on the FIRST load — an append must not blank the table the
          operator is reading; the Load more button disables itself instead. */}
      {loading && orders.length === 0 ? (
        <div className="flex items-center justify-center py-6">
          <RefreshCw className="h-5 w-5 animate-spin text-primary" />
        </div>
      ) : orders.length === 0 ? (
        <div className="py-6 text-center">
          <FileText className="mx-auto mb-2 h-8 w-8 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            No orders recorded for this customer yet.
          </p>
        </div>
      ) : (
        <div>
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead numeric className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Order</TableHead>
                <TableHead className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Type</TableHead>
                <TableHead className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Date</TableHead>
                <TableHead className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Status</TableHead>
                <TableHead numeric className="h-auto px-2 py-2.5 text-xs font-medium text-muted-foreground">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orders.map((order) => {
                const statusTone = ORDER_STATUS_TONE[order.status] ?? 'default';
                const type = presentOrderType(order.orderType);
                return (
                  <TableRow
                    key={order.orderId}
                    onClick={() => onView(order.orderId)}
                    className="cursor-pointer hover:bg-muted"
                  >
                    <TableCell numeric className="px-2 py-2.5">
                      <span className="text-[11px] font-medium text-primary">
                        ORD-{order.orderId.replace(/-/g, '').slice(0, 8).toUpperCase()}
                      </span>
                    </TableCell>
                    <TableCell className="px-2 py-2.5">
                      <Pill tone={type.tone} size="sm">
                        {type.label}
                      </Pill>
                    </TableCell>
                    <TableCell className="px-2 py-2.5">
                      <span className="font-mono text-[11px] tabular-nums text-muted-foreground">
                        {formatDate(order.createdAt)}
                      </span>
                    </TableCell>
                    <TableCell className="px-2 py-2.5">
                      <Pill tone={statusTone} dot size="sm">
                        {order.status}
                      </Pill>
                    </TableCell>
                    <TableCell numeric className="px-2 py-2.5">
                      <span className="text-[12.5px] font-medium text-foreground">
                        {formatCurrency(order.totalAmountIn, order.originCurrency)}
                      </span>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>

          {hasMore && (
            <div className="pt-3 text-center">
              <Button
                type="button"
                variant="link"
                size="sm"
                onClick={onLoadMore}
                disabled={loading}
                className="h-auto p-0 text-xs"
              >
                Load more
              </Button>
            </div>
          )}
        </div>
      )}
    </AonikCard>
  );
}
