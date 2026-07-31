// Commerce products (Spec 082) — the retail catalog list plus the route-addressable editor.
//
// Two things this list deliberately does NOT render, because the summary DTO cannot support
// them truthfully: a retail price (summaries carry none by design — the brand rule is that a
// dish never shows a standalone price) and any "updated" column (there is no timestamp on
// the row). The surcharge shows as a MARKER only: the summary has the amount but not its
// currency, and an amount without its denomination is not a fact worth printing.

import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { AlertCircle, RefreshCw } from 'lucide-react';

import {
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  KpiTile,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import {
  DataTable,
  DataTablePagination,
  type ColumnDef,
} from '@/components/ui/data-table';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import type { PagedResult } from '@/types';
import type { ProductCategoryDto, ProductSummaryDto } from '@/types/commerce';

import { ProductEditorSheet } from './components/ProductEditorSheet';

const KIND_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Simple', label: 'Simple' },
  { value: 'Variant', label: 'Variant' },
  { value: 'Bundle', label: 'Bundle' },
];

const STATUS_TONE: Record<string, PillTone> = {
  Active: 'success',
  Draft: 'warning',
  Archived: 'muted',
};

const STATUSES = ['Active', 'Draft', 'Archived'];

export function CommerceProductsPage() {
  const navigate = useNavigate();
  const { productId } = useParams<{ productId: string }>();

  const [products, setProducts] = useState<ProductSummaryDto[]>([]);
  const [categories, setCategories] = useState<ProductCategoryDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [kindTab, setKindTab] = useState('');
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
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
      const result: PagedResult<ProductSummaryDto> = await commerceCatalogService.listProducts({
        page: pageNumber,
        pageSize,
        kind: kindTab || undefined,
        status: status || undefined,
        search: search || undefined,
      });
      if (requestIdRef.current !== requestId) return;
      setProducts(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load products.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [pageNumber, pageSize, kindTab, status, search]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setPageNumber(1);
  }, [kindTab, status, search]);

  // Categories label the editor's picker; a failure leaves the picker id-only rather than
  // blocking the page.
  useEffect(() => {
    let cancelled = false;
    commerceCatalogService
      .listCategories()
      .then((result) => !cancelled && setCategories(result))
      .catch(() => !cancelled && setCategories([]));
    return () => {
      cancelled = true;
    };
  }, []);

  // KPIs describe the LOADED window, not the whole catalog — the page is paged, and a
  // whole-store claim from one page would be a fabrication.
  const activeCount = products.filter((p) => p.status === 'Active').length;
  const draftCount = products.filter((p) => p.status === 'Draft').length;
  const bundleCount = products.filter((p) => p.kind === 'Bundle').length;

  const columns: ColumnDef<ProductSummaryDto>[] = [
    {
      id: 'product',
      header: 'Product',
      accessorFn: (row) => row.name,
      sortable: true,
      cell: (row) => (
        <div className="flex items-center gap-2.5">
          {row.heroImageUrl ? (
            <img
              src={row.heroImageUrl}
              alt=""
              // Keyed by URL so a repaired image gets a FRESH node: React would otherwise
              // reuse this one and the hidden-on-error inline style would survive the new src,
              // leaving a working thumbnail invisible until the page remounted.
              key={row.heroImageUrl}
              className="h-8 w-8 rounded object-cover"
              onError={(e) => {
                e.currentTarget.style.visibility = 'hidden';
              }}
            />
          ) : (
            <span className="h-8 w-8 rounded bg-[var(--color-surface-inset)]" />
          )}
          <span className="flex min-w-0 flex-col">
            <span className="truncate text-[13px] font-medium text-[var(--color-text-primary)]">
              {row.name}
            </span>
            <span className="truncate font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
              {row.slug}
            </span>
          </span>
        </div>
      ),
      className: 'pl-4',
      headerClassName: 'pl-4',
    },
    {
      id: 'kind',
      header: 'Kind',
      accessorKey: 'kind',
      sortable: true,
      cell: (row) => <span className="text-xs text-[var(--color-text-secondary)]">{row.kind}</span>,
      className: 'w-[110px]',
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (row) => <Pill tone={STATUS_TONE[row.status] ?? 'default'}>{row.status}</Pill>,
      className: 'w-[110px]',
    },
    {
      id: 'variants',
      header: 'Variants',
      accessorFn: (row) => row.variantCount,
      sortable: true,
      cell: (row) => (
        <span className="block text-right font-[family-name:var(--font-mono)] text-xs tabular-nums text-[var(--color-text-secondary)]">
          {row.variantCount}
        </span>
      ),
      className: 'w-[90px] text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'tags',
      header: 'Tags',
      accessorFn: (row) => row.tags.join(','),
      cell: (row) =>
        row.tags.length === 0 ? (
          <span className="text-[var(--color-text-tertiary)]">—</span>
        ) : (
          <span className="flex flex-wrap gap-1">
            {row.tags.slice(0, 3).map((tag) => (
              <Pill key={tag} tone="muted" size="sm">
                {tag}
              </Pill>
            ))}
            {row.tags.length > 3 && (
              <span className="text-[11px] text-[var(--color-text-tertiary)]">
                +{row.tags.length - 3}
              </span>
            )}
          </span>
        ),
      className: 'w-[200px]',
    },
    {
      id: 'surcharge',
      header: 'Surcharge',
      accessorFn: (row) => (row.unitSurcharge != null ? 1 : 0),
      // A MARKER, not an amount: the summary carries no currency, and a bare number would
      // read as a price in whatever currency the operator assumed.
      cell: (row) =>
        row.unitSurcharge != null ? (
          <Pill tone="info" size="sm" dot>
            Set
          </Pill>
        ) : (
          <span className="text-[var(--color-text-tertiary)]">—</span>
        ),
      className: 'w-[110px]',
    },
  ];

  // A deep link to /commerce/products/:id must not wait on the paged list behind it: the two
  // reads are unrelated, and a slow list would hold a healthy detail read hostage.
  const editor = productId ? (
    <ProductEditorSheet
      key={productId}
      productId={productId}
      categories={categories}
      onClose={() => navigate('/commerce/products')}
      onSaved={() => void load()}
    />
  ) : null;

  if (initialLoad) {
    return (
      <>
        <PageLoadingScreen message="Loading products" />
        {editor}
      </>
    );
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Commerce"
        title="Products"
        subtitle="The retail catalogue behind the storefront — products, media and storefront placement"
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label="Products" value={totalCount.toLocaleString()} delta="all pages" deltaTone="neutral" />
        <KpiTile label="Active" value={activeCount.toLocaleString()} delta="this page" deltaTone="neutral" />
        <KpiTile label="Drafts" value={draftCount.toLocaleString()} delta="this page" deltaTone="neutral" />
        <KpiTile label="Bundles" value={bundleCount.toLocaleString()} delta="this page" deltaTone="neutral" />
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
        tabs={KIND_TABS}
        active={kindTab}
        onTabChange={setKindTab}
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Search products"
        // Status is a server-side filter, so it narrows the whole catalogue rather than the
        // loaded page. The bar's default trailing "Filters" button is hidden because there is
        // nothing behind it — an inert control reads as a feature that is broken.
        extra={
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            aria-label="Status"
            className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2 py-1.5 text-[12.5px] text-[var(--color-text-primary)] outline-none"
          >
            <option value="">Any status</option>
            {STATUSES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        }
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
              data={products}
              columns={columns}
              getRowId={(row) => row.id}
              onRowClick={(row) => navigate(`/commerce/products/${row.id}`)}
              emptyTitle="No products"
              emptyDescription="No products match this filter."
              // No bulk workflow here yet, and a checkbox that cannot change state is worse
              // than none — it offers a selection the page will never act on.
              showCheckboxes={false}
            />
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              // A larger page size can put the current page past the new last one, which
              // returns an empty page against a nonzero total — start again from the first.
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPageNumber(1);
              }}
            />
          </>
        )}
      </AonikCard>

      {editor}
    </div>
  );
}
