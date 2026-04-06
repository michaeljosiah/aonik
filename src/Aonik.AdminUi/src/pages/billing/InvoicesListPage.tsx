import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Receipt,
  Plus,
  Eye,
  Send,
  CheckCircle2,
  XCircle,
  AlertCircle,
  FileText,
  DollarSign,
  Clock,
} from 'lucide-react';
import { toast } from 'sonner';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';
import { billingService } from '@/services/billingService';
import type { InvoiceResponse } from '@/types';

// ── Helpers ─────────────────────────────────────────────────────────

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

function shortId(id: string): string {
  if (id.length <= 10) return id;
  return id.slice(0, 8) + '…';
}

const STATUS_STYLES: Record<string, string> = {
  Draft: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400',
  Issued: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  Paid: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
  Cancelled: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
};

function isOverdue(invoice: InvoiceResponse): boolean {
  return invoice.status === 'Issued' && new Date(invoice.dueUtc) < new Date();
}

// ── Component ───────────────────────────────────────────────────────

export function InvoicesListPage() {
  const navigate = useNavigate();
  const [invoices, setInvoices] = useState<InvoiceResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const requestIdRef = useRef(0);

  const loadInvoices = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await billingService.listInvoices(statusFilter || undefined);
      if (requestId !== requestIdRef.current) return;
      setInvoices(result);
    } catch (err: unknown) {
      if (requestId !== requestIdRef.current) return;
      const message = err instanceof Error ? err.message : 'Failed to load invoices';
      setError(message);
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    void loadInvoices();
  }, [loadInvoices]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter]);

  // Client-side search filtering
  const filteredInvoices = useMemo(() => {
    if (!searchQuery.trim()) return invoices;
    const q = searchQuery.toLowerCase();
    return invoices.filter(
      (inv) =>
        inv.invoiceNumber.toLowerCase().includes(q) ||
        inv.currency.toLowerCase().includes(q),
    );
  }, [invoices, searchQuery]);

  // Client-side pagination
  const totalCount = filteredInvoices.length;
  const pagedInvoices = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return filteredInvoices.slice(start, start + pageSize);
  }, [filteredInvoices, pageNumber, pageSize]);

  // ── Summary metrics ───────────────────────────────────────────────

  const metrics = useMemo(() => {
    const now = new Date();
    const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);

    let draftCount = 0;
    let outstandingAmount = 0;
    let overdueAmount = 0;
    let paidThisMonth = 0;

    for (const inv of invoices) {
      if (inv.status === 'Draft') draftCount++;
      if (inv.status === 'Issued') {
        outstandingAmount += inv.totalAmount;
        if (new Date(inv.dueUtc) < now) overdueAmount += inv.totalAmount;
      }
      if (inv.status === 'Paid' && new Date(inv.issuedUtc) >= startOfMonth) {
        paidThisMonth += inv.totalAmount;
      }
    }

    return { draftCount, outstandingAmount, overdueAmount, paidThisMonth };
  }, [invoices]);

  // ── Row actions ───────────────────────────────────────────────────

  const getRowActions = useCallback(
    (invoice: InvoiceResponse): DataTableAction[] => {
      const actions: DataTableAction[] = [
        {
          label: 'View',
          icon: <Eye className="w-4 h-4" />,
          onClick: () => navigate(`/billing/invoices/${invoice.id}`),
        },
      ];

      if (invoice.status === 'Draft') {
        actions.push({
          label: 'Issue',
          icon: <Send className="w-4 h-4" />,
          onClick: async () => {
            try {
              await billingService.issueInvoice(invoice.id);
              toast.success('Invoice issued.');
              void loadInvoices();
            } catch {
              toast.error('Failed to issue invoice.');
            }
          },
        });
      }

      if (invoice.status === 'Issued') {
        actions.push({
          label: 'Mark Paid',
          icon: <CheckCircle2 className="w-4 h-4" />,
          onClick: async () => {
            try {
              await billingService.markPaid(invoice.id);
              toast.success('Invoice marked as paid.');
              void loadInvoices();
            } catch {
              toast.error('Failed to mark invoice as paid.');
            }
          },
        });
      }

      if (invoice.status === 'Draft' || invoice.status === 'Issued') {
        actions.push({
          label: 'Cancel',
          icon: <XCircle className="w-4 h-4" />,
          variant: 'danger' as const,
          onClick: async () => {
            try {
              await billingService.cancelInvoice(invoice.id);
              toast.success('Invoice cancelled.');
              void loadInvoices();
            } catch {
              toast.error('Failed to cancel invoice.');
            }
          },
        });
      }

      return actions;
    },
    [navigate, loadInvoices],
  );

  // ── Column definitions ────────────────────────────────────────────

  const columns: ColumnDef<InvoiceResponse>[] = [
    {
      id: 'invoiceNumber',
      header: 'Invoice',
      accessorFn: (row) => row.invoiceNumber,
      sortable: true,
      cell: (invoice) => (
        <div>
          <div className="font-medium text-[var(--color-text-primary)]">
            {shortId(invoice.invoiceNumber)}
          </div>
          <div className="text-xs text-[var(--color-text-tertiary)]">{invoice.currency}</div>
        </div>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorFn: (row) => row.status,
      sortable: true,
      cell: (invoice) => (
        <Badge className={STATUS_STYLES[invoice.status] ?? STATUS_STYLES.Draft}>
          {invoice.status}
        </Badge>
      ),
    },
    {
      id: 'issuedUtc',
      header: 'Issue Date',
      accessorFn: (row) => row.issuedUtc,
      sortable: true,
      cell: (invoice) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {formatDate(invoice.issuedUtc)}
        </span>
      ),
    },
    {
      id: 'dueUtc',
      header: 'Due Date',
      accessorFn: (row) => row.dueUtc,
      sortable: true,
      cell: (invoice) => (
        <span
          className={`text-sm ${
            isOverdue(invoice)
              ? 'text-red-600 font-medium'
              : 'text-[var(--color-text-secondary)]'
          }`}
        >
          {formatDate(invoice.dueUtc)}
          {isOverdue(invoice) && (
            <span className="ml-1 text-xs text-red-500">Overdue</span>
          )}
        </span>
      ),
    },
    {
      id: 'totalAmount',
      header: 'Amount',
      accessorFn: (row) => row.totalAmount,
      sortable: true,
      headerClassName: 'text-right',
      className: 'text-right',
      cell: (invoice) => (
        <span className="font-medium text-[var(--color-text-primary)]">
          {formatMoney(invoice.totalAmount, invoice.currency)}
        </span>
      ),
    },
  ];

  // ── Render ────────────────────────────────────────────────────────

  const breadcrumbItems = [
    { label: 'Billing', href: '/billing/invoices', icon: <Receipt className="w-3.5 h-3.5" /> },
    { label: 'Invoices', icon: <FileText className="w-3.5 h-3.5" /> },
  ];

  const statusFilterOptions = [
    { label: 'All Statuses', value: '__all__' },
    { label: 'Draft', value: 'Draft' },
    { label: 'Issued', value: 'Issued' },
    { label: 'Paid', value: 'Paid' },
    { label: 'Cancelled', value: 'Cancelled' },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Invoices</h1>
          <p className="text-[var(--color-text-secondary)]">
            Create, manage, and track invoices across your billing operations.
          </p>
        </div>
        <Button onClick={() => navigate('/billing/invoices/new')}>
          <Plus className="w-4 h-4 mr-2" />
          New Invoice
        </Button>
      </div>

      {/* Summary metrics */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <Card>
          <CardContent className="pt-4 pb-4">
            <div className="flex items-center gap-3">
              <div className="rounded-md bg-zinc-100 dark:bg-zinc-800 p-2">
                <FileText className="w-4 h-4 text-zinc-600 dark:text-zinc-400" />
              </div>
              <div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Drafts</div>
                <div className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {metrics.draftCount}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4 pb-4">
            <div className="flex items-center gap-3">
              <div className="rounded-md bg-blue-100 dark:bg-blue-900/30 p-2">
                <DollarSign className="w-4 h-4 text-blue-600 dark:text-blue-400" />
              </div>
              <div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Outstanding</div>
                <div className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {formatMoney(metrics.outstandingAmount, 'USD')}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4 pb-4">
            <div className="flex items-center gap-3">
              <div className="rounded-md bg-red-100 dark:bg-red-900/30 p-2">
                <Clock className="w-4 h-4 text-red-600 dark:text-red-400" />
              </div>
              <div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Overdue</div>
                <div className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {formatMoney(metrics.overdueAmount, 'USD')}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4 pb-4">
            <div className="flex items-center gap-3">
              <div className="rounded-md bg-emerald-100 dark:bg-emerald-900/30 p-2">
                <CheckCircle2 className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
              </div>
              <div>
                <div className="text-xs text-[var(--color-text-tertiary)]">Paid this month</div>
                <div className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {formatMoney(metrics.paidThisMonth, 'USD')}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {error && (
        <Card className="mb-6 border-red-200 bg-red-50/60 dark:border-red-900/30 dark:bg-red-950/10">
          <CardContent className="pt-4 pb-4 flex items-center gap-3 text-sm text-red-700 dark:text-red-300">
            <AlertCircle className="w-4 h-4" />
            <span>{error}</span>
            <Button size="sm" variant="outline" onClick={() => void loadInvoices()} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <DataTableHeader
        searchValue={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Search invoices..."
        filterValue={statusFilter || '__all__'}
        onFilterChange={(v) => setStatusFilter(v === '__all__' ? '' : v)}
        filterOptions={statusFilterOptions}
        showViewToggle={false}
      />

      <Card className="mt-4">
        <CardContent className="p-0">
          <DataTable
            data={pagedInvoices}
            columns={columns}
            getRowId={(inv) => inv.id}
            selectedIds={selectedIds}
            onSelectionChange={setSelectedIds}
            showCheckboxes={false}
            loading={loading}
            onRowClick={(invoice) => navigate(`/billing/invoices/${invoice.id}`)}
            rowActions={(invoice) => (
              <DataTableRowActions actions={getRowActions(invoice)} />
            )}
          />
        </CardContent>
      </Card>

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
    </div>
  );
}
