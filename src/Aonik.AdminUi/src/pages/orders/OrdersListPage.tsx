// Orders list — visual port of the ScreenOrders half of
// templates/aonik-admin-starterkit/screens/customers-orders.jsx, wired to
// the existing /orders endpoint.
//
// Differences from the template, called out so they don't read as gaps:
//   • The 4 mini-stat tiles say "all time" rather than "today" because the
//     /orders endpoint can't filter by date today. Counts come from four
//     small concurrent listOrders(pageSize:1) calls per status. Replace
//     with a stats endpoint when one ships.
//   • Rail / FX column maps to OriginCurrency → DestinationCurrency when
//     both are set; otherwise just OriginCurrency.

import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Plus, RefreshCw } from 'lucide-react';

import {
  AgentAvatar,
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import {
  DataTable,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { orderService, type ListOrdersParams } from '@/services/orderService';
import type { OrderListItem, PagedResult } from '@/types';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatMoney(amount: number | null | undefined, currency?: string | null): string {
  if (amount == null) return '—';
  const cur = (currency ?? '').trim();
  if (!cur) return amount.toLocaleString();
  return `${cur} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function shortOrderId(orderId: string): string {
  const compact = orderId.replace(/-/g, '').slice(0, 8).toUpperCase();
  return `ORD-${compact}`;
}

const STATUS_TONE: Record<string, PillTone> = {
  Draft: 'muted',
  PendingCompliance: 'warning',
  Submitted: 'info',
  Active: 'info',
  Captured: 'success',
  Settled: 'success',
  Complete: 'success',
  Completed: 'success',
  Cancelled: 'muted',
  Failed: 'danger',
  Expired: 'muted',
};

const FILTER_TABS: FilterBarTab[] = [
  { value: 'all', label: 'All' },
  { value: 'type:BillPayment', label: 'Bill payments' },
  { value: 'status:Submitted', label: 'Submitted' },
  { value: 'status:PendingCompliance', label: 'Pending compliance' },
  { value: 'status:Failed', label: 'Failed' },
];

interface OrderStatBucket {
  /** Tab value mapped from the same encoding as FILTER_TABS. */
  key: string;
  label: string;
  status?: string;
  orderType?: string;
  /** Brand colour for the leading dot. */
  tone: string;
}

const STAT_BUCKETS: OrderStatBucket[] = [
  { key: 'settled', label: 'Settled', status: 'Complete', tone: 'var(--color-success)' },
  { key: 'inflight', label: 'In flight', status: 'Submitted', tone: 'var(--color-brand-primary)' },
  {
    key: 'pending',
    label: 'Pending compliance',
    status: 'PendingCompliance',
    tone: 'var(--color-warning)',
  },
  { key: 'failed', label: 'Failed', status: 'Failed', tone: 'var(--color-danger)' },
];

// ─── Page ────────────────────────────────────────────────────────────────

export function OrdersListPage() {
  const navigate = useNavigate();

  const [orders, setOrders] = useState<OrderListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [activeTab, setActiveTab] = useState<string>('all');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [stats, setStats] = useState<Record<string, number>>({});
  const [statsLoading, setStatsLoading] = useState(false);

  const requestIdRef = useRef(0);

  // Tab encoding: "type:X" → orderType filter; "status:X" → status filter;
  // "all" → no filter.
  const tabFilter = (() => {
    if (activeTab.startsWith('type:')) return { orderType: activeTab.slice(5) };
    if (activeTab.startsWith('status:')) return { status: activeTab.slice(7) };
    return {};
  })();

  const loadOrders = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const params: ListOrdersParams = {
        pageNumber,
        pageSize,
        search: searchQuery || undefined,
        ...tabFilter,
      };
      const result: PagedResult<OrderListItem> = await orderService.listOrders(params);
      if (requestIdRef.current !== requestId) return;
      setOrders(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load orders. Please try again.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageNumber, pageSize, searchQuery, activeTab]);

  const loadStats = useCallback(async () => {
    setStatsLoading(true);
    // "Today" = orders created since 00:00 local. The backend filter is
    // inclusive on the lower bound and exclusive on the upper bound, so we
    // pass start-of-today and start-of-tomorrow.
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    const startOfTomorrow = new Date(startOfToday);
    startOfTomorrow.setDate(startOfTomorrow.getDate() + 1);

    try {
      const results = await Promise.all(
        STAT_BUCKETS.map((bucket) =>
          orderService.listOrders({
            pageSize: 1,
            status: bucket.status,
            orderType: bucket.orderType,
            createdFromUtc: startOfToday.toISOString(),
            createdToUtc: startOfTomorrow.toISOString(),
          }),
        ),
      );
      const next: Record<string, number> = {};
      STAT_BUCKETS.forEach((bucket, idx) => {
        next[bucket.key] = results[idx].totalCount;
      });
      setStats(next);
    } catch {
      // Stats are decorative — silent fail keeps the table working.
      setStats({});
    } finally {
      setStatsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadOrders();
  }, [loadOrders]);

  useEffect(() => {
    void loadStats();
  }, [loadStats]);

  // Reset to page 1 whenever the filter changes.
  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, activeTab]);

  if (initialLoad) {
    return <PageLoadingScreen message="Loading orders" />;
  }

  // ─── Columns ──────────────────────────────────────────────────────────

  const columns: ColumnDef<OrderListItem>[] = [
    {
      id: 'order',
      header: 'Order',
      accessorKey: 'orderId',
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[11px] font-medium text-[var(--color-brand-primary)]">
          {shortOrderId(row.orderId)}
        </span>
      ),
      className: 'w-[140px] pl-4',
      headerClassName: 'pl-4',
    },
    {
      id: 'date',
      header: 'Submitted',
      accessorFn: (row) => (row.createdAt ? new Date(row.createdAt) : null),
      sortable: true,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {formatDate(row.createdAt)}
        </span>
      ),
      className: 'w-[120px]',
    },
    {
      id: 'type',
      header: 'Type',
      accessorKey: 'orderType',
      sortable: true,
      cell: (row) => (
        <span className="text-xs text-[var(--color-text-secondary)]">{row.orderType}</span>
      ),
      className: 'w-[120px]',
    },
    {
      id: 'party',
      header: 'Counterparty',
      accessorFn: (row) => row.payerName ?? '',
      sortable: true,
      cell: (row) => (
        <div className="flex items-center gap-2.5">
          <AgentAvatar name={row.payerName || 'Unknown'} size={22} />
          <span className="truncate text-[13px] text-[var(--color-text-primary)]">
            {row.payerName || '—'}
          </span>
        </div>
      ),
    },
    {
      id: 'rail',
      header: 'Rail / FX',
      accessorFn: (row) =>
        row.destinationCurrency
          ? `${row.originCurrency}→${row.destinationCurrency}`
          : row.originCurrency,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {row.originCountry ? `${row.originCountry} · ` : ''}
          {row.destinationCurrency
            ? `${row.originCurrency}→${row.destinationCurrency}`
            : row.originCurrency}
        </span>
      ),
      className: 'w-[140px]',
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (row) => (
        <Pill tone={STATUS_TONE[row.status] ?? 'default'} dot>
          {row.status}
        </Pill>
      ),
      className: 'w-[140px]',
    },
    {
      id: 'amount',
      header: 'Amount',
      accessorFn: (row) => row.totalAmountIn,
      sortable: true,
      cell: (row) => (
        <span className="block text-right font-[family-name:var(--font-mono)] text-[12.5px] font-semibold text-[var(--color-text-primary)]">
          {formatMoney(row.totalAmountIn, row.originCurrency)}
        </span>
      ),
      className: 'w-[140px] text-right',
      headerClassName: 'text-right',
    },
  ];

  const rowActions = (order: OrderListItem): DataTableAction[] => [
    {
      label: 'View order',
      onClick: () => {
        if (order.orderType === 'BillPayment') {
          navigate(`/orders/bill-payments/${order.orderId}`);
        }
      },
      disabled: order.orderType !== 'BillPayment',
    },
  ];

  // ─── Header counts ────────────────────────────────────────────────────

  const subtitle = totalCount > 0
    ? `${totalCount.toLocaleString()} total · bill payments, payouts, and collections`
    : 'Bill payments, payouts, and collections';

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Finance · Orders"
        title="Orders"
        subtitle={subtitle}
        actions={
          <>
            <Button variant="outline" size="sm" onClick={() => void loadStats()} disabled={statsLoading}>
              <RefreshCw className={'h-3 w-3 ' + (statsLoading ? 'animate-spin' : '')} />
              Refresh
            </Button>
            <Button size="sm" onClick={() => navigate('/orders/bill-payments/new')}>
              <Plus className="h-3 w-3" />
              New bill payment
            </Button>
          </>
        }
      />

      {/* Mini stats — 4 status buckets, all-time counts (date filter not yet supported) */}
      <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-4">
        {STAT_BUCKETS.map((bucket) => (
          <button
            key={bucket.key}
            type="button"
            onClick={() => {
              if (bucket.status) {
                setActiveTab(`status:${bucket.status}`);
              } else if (bucket.orderType) {
                setActiveTab(`type:${bucket.orderType}`);
              }
            }}
            className="flex flex-col items-start gap-1 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3.5 text-left transition-colors hover:bg-[var(--color-surface-inset)]"
          >
            <div className="flex items-center gap-1.5 text-[11px] text-[var(--color-text-secondary)]">
              <span
                className="h-1.5 w-1.5 rounded-full"
                style={{ background: bucket.tone }}
              />
              {bucket.label}
            </div>
            <div className="font-[family-name:var(--font-mono)] text-[22px] font-semibold leading-none text-[var(--color-text-primary)]">
              {statsLoading
                ? '—'
                : (stats[bucket.key] ?? 0).toLocaleString()}
            </div>
            <div className="font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
              today · click to filter status
            </div>
          </button>
        ))}
      </div>

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadOrders()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={activeTab}
        onTabChange={setActiveTab}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter by order ref, party, amount…"
        hideFilterButton
      />

      <AonikCard padding={0}>
        <DataTable
          data={orders}
          columns={columns}
          getRowId={(o) => o.orderId}
          showCheckboxes={false}
          loading={loading}
          loadingMessage="Loading orders…"
          emptyTitle="No orders found"
          emptyDescription={
            searchQuery || activeTab !== 'all'
              ? 'Try adjusting the active tab or search.'
              : 'Orders will appear here as they are created.'
          }
          rowActions={(o) => <DataTableRowActions actions={rowActions(o)} />}
          rowActionsPosition="end"
          onRowClick={(order) => {
            if (order.orderType === 'BillPayment') {
              navigate(`/orders/bill-payments/${order.orderId}`);
            }
          }}
        />
        <DataTablePagination
          pageNumber={pageNumber}
          pageSize={pageSize}
          totalCount={totalCount}
          onPageChange={setPageNumber}
          onPageSizeChange={(n) => {
            setPageSize(n);
            setPageNumber(1);
          }}
          className="border-t border-[var(--color-border-light)]"
        />
      </AonikCard>
    </div>
  );
}
