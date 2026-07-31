// Commerce orders (Spec 083 §2) — checked-out storefront orders plus the route-addressable
// order drawer.
//
// Every KPI here describes the LOADED WINDOW and says so in its caption. The list is paged,
// so a whole-store figure cannot be computed from it, and a caption-less number would read
// as one. Revenue is summed per currency for the same reason a total is never summed across
// currencies anywhere in this series: there is no rate here, and adding them would invent one.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { AlertCircle, RefreshCw } from 'lucide-react';

import {
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  KpiTile,
  PageHeader,
  Pill,
} from '@/components/layout/aonik';
import { DataTable, DataTablePagination, type ColumnDef } from '@/components/ui/data-table';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { commerceStorefrontService } from '@/services/commerceStorefrontService';
import { formatCurrency, formatDate } from '@/lib/format';
import type { PagedResult } from '@/types';
import type { AdminStorefrontOrderRowDto } from '@/types/commerce';

import { BuyerLabel } from './components/BuyerLabel';
import { CommerceTabs } from './components/CommerceTabs';
import { OrderDrawer } from './components/OrderDrawer';
import { summariseOrderWindow } from './lib/orderWindow';
import { fulfilmentTone, paymentTone } from './lib/statusTone';

const PAYMENT_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Captured', label: 'Captured' },
  { value: 'Pending', label: 'Pending' },
];

export function CommerceOrdersPage() {
  const navigate = useNavigate();
  const { orderId } = useParams<{ orderId: string }>();

  const [orders, setOrders] = useState<AdminStorefrontOrderRowDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [paymentStatus, setPaymentStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestIdRef = useRef(0);

  const load = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<AdminStorefrontOrderRowDto> =
        await commerceStorefrontService.listStorefrontOrders({
          page: pageNumber,
          pageSize,
          paymentStatus: paymentStatus || undefined,
        });
      if (requestIdRef.current !== requestId) return;
      const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
      if (pageNumber > lastPage) {
        setTotalCount(result.totalCount);
        setPageNumber(lastPage);
        return;
      }
      setOrders(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      // The filter control already shows the new query, so keeping the previous rows would
      // present them as its results.
      setOrders([]);
      setTotalCount(0);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load orders.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [pageNumber, pageSize, paymentStatus]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setPageNumber(1);
  }, [paymentStatus]);

  const summary = useMemo(() => summariseOrderWindow(orders), [orders]);

  const columns: ColumnDef<AdminStorefrontOrderRowDto>[] = [
    {
      id: 'order',
      header: 'Order',
      accessorFn: (row) => row.orderId,
      cell: (row) => (
        <span className="flex flex-col">
          <span className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">
            {row.orderId.slice(0, 8)}
          </span>
          {row.boxSize != null && (
            <span className="text-[11px] text-[var(--color-text-tertiary)]">
              Box of {row.boxSize}
            </span>
          )}
        </span>
      ),
      className: 'pl-4 w-[150px]',
      headerClassName: 'pl-4',
    },
    {
      id: 'buyer',
      header: 'Buyer',
      accessorFn: (row) => row.buyerKind,
      cell: (row) => <BuyerLabel buyerKind={row.buyerKind} buyerPartyId={row.buyerPartyId} />,
      className: 'w-[150px]',
    },
    {
      id: 'total',
      header: 'Total',
      accessorFn: (row) => row.total,
      cell: (row) => (
        <span className="block text-right font-[family-name:var(--font-mono)] text-[12.5px] tabular-nums text-[var(--color-text-primary)]">
          {formatCurrency(row.total, row.currency)}
        </span>
      ),
      className: 'w-[130px] text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'payment',
      header: 'Payment',
      accessorKey: 'paymentStatus',
      cell: (row) => <Pill tone={paymentTone(row.paymentStatus)}>{row.paymentStatus}</Pill>,
      className: 'w-[120px]',
    },
    {
      id: 'fulfilment',
      header: 'Fulfilment',
      accessorKey: 'fulfilmentStatus',
      cell: (row) => (
        <Pill tone={fulfilmentTone(row.fulfilmentStatus)}>{row.fulfilmentStatus}</Pill>
      ),
      className: 'w-[130px]',
    },
    {
      id: 'placed',
      header: 'Placed',
      accessorFn: (row) => row.placedAtUtc,
      cell: (row) => (
        <span className="text-[12px] text-[var(--color-text-secondary)]">
          {formatDate(row.placedAtUtc)}
        </span>
      ),
      className: 'w-[130px]',
    },
  ];

  const drawer = orderId ? (
    <OrderDrawer
      key={orderId}
      orderId={orderId}
      // REPLACE, not push: the drawer route is a layer over this list, so pushing the list
      // back on would let Back reopen the order just closed.
      onClose={() => navigate('/commerce/orders', { replace: true })}
    />
  ) : null;

  if (initialLoad) {
    return (
      <>
        <PageLoadingScreen message="Loading orders" />
        {drawer}
      </>
    );
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Commerce"
        title="Orders"
        subtitle="Checked-out storefront orders — what was ordered, what was charged, and what the kitchen prepares"
      />

      <CommerceTabs active="orders" />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile
          label="Orders"
          value={totalCount.toLocaleString()}
          delta="all pages"
          deltaTone="neutral"
        />
        <KpiTile
          label="Paid revenue"
          value={summary.paidRevenue}
          delta={summary.moneyCaption}
          deltaTone="neutral"
        />
        <KpiTile
          label="Average paid order"
          value={summary.averageOrder}
          delta={summary.moneyCaption}
          deltaTone="neutral"
        />
        {/* Spec 083 names an "awaiting fulfilment" tile. It is not shipped: fulfilment is
            underived (there is no Fulfilled status to reach), so that count would equal the
            order count while implying delivery is tracked. Awaiting PAYMENT is derivable,
            actionable and true — see lib/orderWindow.ts. */}
        <KpiTile
          label="Awaiting payment"
          value={summary.awaitingPayment.toLocaleString()}
          delta="this page"
          deltaTone={summary.awaitingPayment > 0 ? 'down' : 'neutral'}
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4" />
          {error}
          <button type="button" onClick={() => void load()} className="ml-auto underline">
            Retry
          </button>
        </div>
      )}

      <FilterBar
        tabs={PAYMENT_TABS}
        active={paymentStatus}
        onTabChange={setPaymentStatus}
        hideFilterButton
      />

      <AonikCard padding={0}>
        {loading ? (
          <div className="flex items-center justify-center py-10">
            <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
          </div>
        ) : (
          <>
            <DataTable
              data={orders}
              columns={columns}
              getRowId={(row) => row.orderId}
              onRowClick={(row) => navigate(`/commerce/orders/${row.orderId}`)}
              emptyTitle="No orders"
              emptyDescription="No storefront orders match this filter."
              showCheckboxes={false}
            />
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPageNumber(1);
              }}
            />
          </>
        )}
      </AonikCard>

      {drawer}
    </div>
  );
}
