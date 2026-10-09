import { useEffect, useState } from 'react';
import { Plus, RefreshCw } from 'lucide-react';
import { Card as AonikCard, FilterBar, PageHeader, Pill } from '@/components/layout/aonik';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { DataTable, DataTablePagination, type ColumnDef } from '@/components/ui/data-table';
import { commerceDiscountService } from '@/services/commerceDiscountService';
import type { DiscountDto } from '@/types/commerce';
import { DiscountEditorDialog } from './components/DiscountEditorDialog';

export function CommerceDiscountsPage() {
  const [discounts, setDiscounts] = useState<DiscountDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [editor, setEditor] = useState<DiscountDto | 'new' | null>(null);
  const reload = () => { setLoading(true); setRevision((value) => value + 1); };

  useEffect(() => {
    let cancelled = false;
    const timer = setTimeout(() => {
      setError(null);
      commerceDiscountService.list({ page, pageSize, search: search.trim() || undefined,
        isActive: status === 'all' ? undefined : status === 'active' })
        .then((result) => {
          if (cancelled) return;
          const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize));
          if (page > lastPage) { setPage(lastPage); return; }
          setDiscounts(result.items);
          setTotalCount(result.totalCount);
          setLoading(false);
        })
        .catch((err: unknown) => {
          if (cancelled) return;
          setDiscounts([]);
          setTotalCount(0);
          setError(err && typeof err === 'object' && 'userMessage' in err
            ? String(err.userMessage) : 'Discount codes could not be loaded.');
          setLoading(false);
        });
    }, 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [page, pageSize, search, status, revision]);

  // This endpoint pages on the server; sorting only the loaded window would be misleading.
  const columns: ColumnDef<DiscountDto>[] = [
    { id: 'code', header: 'Code', accessorKey: 'code', cell: (row) => (
      <Button variant="link" className="h-auto p-0 font-mono" onClick={() => setEditor(row)}>{row.code}</Button>
    ) },
    { id: 'value', header: 'Discount', accessorKey: 'value', cell: (row) => (
      <span className="tabular-nums">{row.kind === 'Percentage' ? `${row.value}%` : `${row.value} ${row.currency ?? ''}`}</span>
    ) },
    { id: 'active', header: 'Active', accessorKey: 'isActive', cell: (row) => (
      <Pill tone={row.isActive ? 'success' : 'muted'}>{row.isActive ? 'Active' : 'Inactive'}</Pill>
    ) },
    { id: 'usage', header: 'Redemptions', cell: (row) => (
      <div className="text-xs"><p>{row.timesRedeemed} completed · {row.reservedCount} reserved</p>
        <p className="text-muted-foreground">{row.maxRedemptions == null ? 'No limit' : `Limit ${row.maxRedemptions}`}</p></div>
    ) },
    { id: 'products', header: 'Eligibility', cell: (row) => (
      <span className="text-xs">{row.eligibleProductIds == null ? 'All eligible goods' : `${row.eligibleProductIds.length} selected products`}</span>
    ) },
    { id: 'expires', header: 'Expiry (UTC)', cell: (row) => (
      <span className="text-xs">{row.expiresAt ? row.expiresAt.replace('T', ' ').replace(/Z$/, '') : 'No expiry'}</span>
    ) },
  ];

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader title="Discount codes" subtitle="Manage discounts and product eligibility for the storefront"
        actions={<Button onClick={() => setEditor('new')}><Plus aria-hidden />New code</Button>} />
      <FilterBar tabs={[{ value: 'all', label: 'All' }, { value: 'active', label: 'Active' }, { value: 'inactive', label: 'Inactive' }]}
        active={status} onTabChange={(value) => { if (value !== status) { setStatus(value); setPage(1); setLoading(true); } }}
        search={search} searchPlaceholder="Search discount codes"
        onSearchChange={(value) => { const bounded = value.slice(0, 64); if (bounded !== search) { setSearch(bounded); setPage(1); setLoading(true); } }} />
      {error && <Alert variant="destructive"><AlertDescription>
        {error} <Button variant="link" size="sm" onClick={reload}>Retry</Button>
      </AlertDescription></Alert>}
      <AonikCard padding={0}>
        {loading ? <div role="status" className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
          <RefreshCw className="size-4 animate-spin" aria-hidden />Loading discount codes…
        </div> : <>
          <DataTable data={discounts} columns={columns} getRowId={(row) => row.id} showCheckboxes={false}
            emptyTitle="No discount codes" emptyDescription="Create a code or change the filters." />
          <DataTablePagination pageNumber={page} pageSize={pageSize} totalCount={totalCount}
            onPageChange={(value) => { if (value !== page) { setPage(value); setLoading(true); } }}
            onPageSizeChange={(value) => { if (value !== pageSize) { setPageSize(value); setPage(1); setLoading(true); } }} />
        </>}
      </AonikCard>
      {editor && <DiscountEditorDialog key={editor === 'new' ? 'new' : editor.id} discount={editor === 'new' ? undefined : editor}
        onClose={() => setEditor(null)} onSaved={reload} onReload={reload} />}
    </div>
  );
}
