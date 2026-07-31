// Commerce carts (Spec 083 §3) — which sessions are live, stuck or recoverable.
//
// The blocked verdict on this page comes from ONE pure function (lib/cartState). The column,
// the drawer banner and the drawer's disabled footer action are three renderings of it, never
// three re-derivations — the review-hardened rule is that the UI must not offer an operation
// the Spec 068 rules block, and three copies of a rule eventually disagree.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import type { AdminCartRowDto } from '@/types/commerce';

import { BuyerLabel } from './components/BuyerLabel';
import { CartDrawer } from './components/CartDrawer';
import { CommerceTabs } from './components/CommerceTabs';
import { cartBlocked, formatBoxFill } from './lib/cartState';
import { summariseCartWindow } from './lib/cartWindow';
import { cartStatusTone } from './lib/statusTone';

const STATUS_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Open', label: 'Open' },
  { value: 'CheckedOut', label: 'Checked out' },
  { value: 'Abandoned', label: 'Abandoned' },
];

export function CommerceCartsPage() {
  const [carts, setCarts] = useState<AdminCartRowDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [status, setStatus] = useState('');
  const [openCartId, setOpenCartId] = useState<string | null>(null);
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
      const result: PagedResult<AdminCartRowDto> = await commerceStorefrontService.listCarts({
        page: pageNumber,
        pageSize,
        status: status || undefined,
      });
      if (requestIdRef.current !== requestId) return;
      const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
      if (pageNumber > lastPage) {
        setTotalCount(result.totalCount);
        setPageNumber(lastPage);
        return;
      }
      setCarts(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      setCarts([]);
      setTotalCount(0);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load carts.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [pageNumber, pageSize, status]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setPageNumber(1);
  }, [status]);

  const summary = useMemo(() => summariseCartWindow(carts), [carts]);

  const columns: ColumnDef<AdminCartRowDto>[] = [
    {
      id: 'cart',
      header: 'Cart',
      accessorFn: (row) => row.cartId,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">
          {row.cartId.slice(0, 8)}
        </span>
      ),
      className: 'pl-4 w-[120px]',
      headerClassName: 'pl-4',
    },
    {
      id: 'buyer',
      header: 'Buyer',
      accessorFn: (row) => row.buyerKind,
      cell: (row) => <BuyerLabel buyerKind={row.buyerKind} buyerPartyId={row.buyerPartyId} />,
      className: 'w-[140px]',
    },
    {
      id: 'box',
      header: 'Box',
      accessorFn: (row) => (row.boxMeta ? row.boxMeta.filled : -1),
      cell: (row) => {
        const verdict = cartBlocked(row.boxMeta);
        return (
          <span className="flex flex-col">
            <span className="font-[family-name:var(--font-mono)] text-[12px] tabular-nums text-[var(--color-text-secondary)]">
              {formatBoxFill(row.boxMeta)}
            </span>
            {/* Only drift earns the warn line here: an under-filled box is the NORMAL state of
                a cart still being built, and flagging it would cry wolf on every live session.
                The drawer states both causes, where the operator is actually diagnosing. */}
            {row.boxMeta?.drift && (
              <span className="text-[11px] text-[var(--color-warning)]">
                drift — checkout blocked
              </span>
            )}
            {verdict.blocked && !row.boxMeta?.drift && (
              <span className="text-[11px] text-[var(--color-text-tertiary)]">still filling</span>
            )}
          </span>
        );
      },
      className: 'w-[150px]',
    },
    {
      id: 'items',
      header: 'Items',
      accessorFn: (row) => row.itemCount,
      cell: (row) => (
        <span className="block text-right font-[family-name:var(--font-mono)] text-[12px] tabular-nums text-[var(--color-text-secondary)]">
          {row.itemCount}
        </span>
      ),
      className: 'w-[80px] text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'value',
      header: 'Value',
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
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      cell: (row) => <Pill tone={cartStatusTone(row.status)}>{row.status}</Pill>,
      className: 'w-[130px]',
    },
    {
      id: 'activity',
      header: 'Activity',
      accessorFn: (row) => row.updatedAtUtc,
      cell: (row) => (
        <span className="text-[12px] text-[var(--color-text-secondary)]">
          {formatDate(row.updatedAtUtc)}
        </span>
      ),
      className: 'w-[130px]',
    },
  ];

  if (initialLoad) {
    return <PageLoadingScreen message="Loading carts" />;
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Commerce"
        title="Carts"
        subtitle="Live, stuck and recoverable box sessions — with the drift flags computed at load and never persisted"
      />

      <CommerceTabs active="carts" />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {/* When a status filter is applied the total describes THAT status across all pages;
            unfiltered it is every cart. The caption says which, rather than leaving the
            operator to infer it from the filter bar. */}
        <KpiTile
          label={status ? `${status} carts` : 'Carts'}
          value={totalCount.toLocaleString()}
          delta="all pages"
          deltaTone="neutral"
        />
        <KpiTile
          label="Checkout blocked"
          value={summary.blocked.toLocaleString()}
          delta="this page"
          deltaTone={summary.blocked > 0 ? 'down' : 'neutral'}
        />
        <KpiTile
          label="Open value"
          value={summary.openValue}
          delta={summary.moneyCaption}
          deltaTone="neutral"
        />
        <KpiTile
          label="Abandoned"
          value={summary.abandoned.toLocaleString()}
          delta="this page"
          deltaTone={summary.abandoned > 0 ? 'down' : 'neutral'}
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

      <FilterBar tabs={STATUS_TABS} active={status} onTabChange={setStatus} hideFilterButton />

      <AonikCard padding={0}>
        {loading ? (
          <div className="flex items-center justify-center py-10">
            <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
          </div>
        ) : (
          <>
            <DataTable
              data={carts}
              columns={columns}
              getRowId={(row) => row.cartId}
              onRowClick={(row) => setOpenCartId(row.cartId)}
              emptyTitle="No carts"
              emptyDescription="No carts match this filter."
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

      {openCartId && (
        <CartDrawer key={openCartId} cartId={openCartId} onClose={() => setOpenCartId(null)} />
      )}
    </div>
  );
}
