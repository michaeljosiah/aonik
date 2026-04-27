// Invoices list — visual port of ScreenInvoices in
// templates/aonik-admin-starterkit/screens/invoices-accounts.jsx, wired to
// the existing /billing/invoices endpoint.
//
// Differences from the template, called out so they don't read as gaps:
//   • Counterparty column shows a truncated customerId because the
//     InvoiceResponse DTO does not include party display name. Lookup is
//     deferred until the billing list endpoint is enriched.
//   • The template's "Memo" column with agent-suggestion sparkles maps to
//     a relative "Issued / Due" hint here; a true memo field + invoice-
//     level agent proposals are not yet wired.
//   • Overdue is computed client-side (status=Issued & dueUtc<now) — same
//     as the previous shadcn page.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Plus, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import {
  AgentAvatar,
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import {
  DataTable,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';
import { Button } from '@/components/ui/button';
import { billingService } from '@/services/billingService';
import type { InvoiceResponse } from '@/types';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  }
}

function shortInvoiceNumber(num: string): string {
  // Most tenants use sequential invoice numbers (INV-2041); render as-is
  // when short enough, otherwise truncate.
  if (num.length <= 12) return num;
  return `${num.slice(0, 10)}…`;
}

function shortPartyId(partyId: string): string {
  const compact = partyId.replace(/-/g, '').slice(0, 8).toUpperCase();
  return `CUS-${compact}`;
}

/** Display label preferring real party name, falling back to truncated ID. */
function partyDisplay(invoice: InvoiceResponse): { label: string; sub?: string } {
  if (invoice.customerName && invoice.customerName.trim().length > 0) {
    return {
      label: invoice.customerName,
      sub: invoice.customerPartyId ? shortPartyId(invoice.customerPartyId) : undefined,
    };
  }
  // Backend couldn't resolve the party — show the customerAccountId as a
  // last-resort handle so each row is still distinguishable.
  return { label: shortPartyId(invoice.customerId) };
}

const STATUS_TONE: Record<string, PillTone> = {
  Draft: 'muted',
  Issued: 'info',
  Paid: 'success',
  Cancelled: 'muted',
  Overdue: 'danger',
};

const FILTER_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Issued', label: 'Issued' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Cancelled', label: 'Cancelled' },
];

function isOverdue(invoice: InvoiceResponse): boolean {
  return invoice.status === 'Issued' && new Date(invoice.dueUtc) < new Date();
}

// ─── Page ────────────────────────────────────────────────────────────────

export function InvoicesListPage() {
  const navigate = useNavigate();

  const [invoices, setInvoices] = useState<InvoiceResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const requestIdRef = useRef(0);

  const loadInvoices = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await billingService.listInvoices(statusFilter || undefined);
      if (requestIdRef.current !== requestId) return;
      setInvoices(result);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message = err instanceof Error ? err.message : 'Failed to load invoices';
      setError(message);
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    void loadInvoices();
  }, [loadInvoices]);

  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return invoices;
    return invoices.filter(
      (inv) =>
        inv.invoiceNumber.toLowerCase().includes(q) ||
        inv.currency.toLowerCase().includes(q) ||
        inv.customerId.toLowerCase().includes(q) ||
        (inv.customerPartyId ?? '').toLowerCase().includes(q) ||
        (inv.customerName ?? '').toLowerCase().includes(q),
    );
  }, [invoices, searchQuery]);

  const summary = useMemo(() => {
    let outstandingByCurrency = new Map<string, number>();
    let overdueCount = 0;
    let paidCount = 0;
    for (const inv of invoices) {
      if (inv.status === 'Issued') {
        outstandingByCurrency.set(
          inv.currency,
          (outstandingByCurrency.get(inv.currency) ?? 0) + inv.totalAmount,
        );
        if (new Date(inv.dueUtc) < new Date()) overdueCount += 1;
      }
      if (inv.status === 'Paid') paidCount += 1;
    }
    return { outstandingByCurrency, overdueCount, paidCount, total: invoices.length };
  }, [invoices]);

  const subtitle = (() => {
    if (invoices.length === 0) return 'Customer invoices · billing module';
    const outstandingParts = Array.from(summary.outstandingByCurrency.entries()).map(
      ([cur, amt]) => formatMoney(amt, cur),
    );
    const outstandingFragment = outstandingParts.length > 0
      ? ` · ${outstandingParts.join(' / ')} outstanding`
      : '';
    const overdueFragment = summary.overdueCount > 0
      ? ` · ${summary.overdueCount} overdue`
      : '';
    return `${summary.total.toLocaleString()} total${outstandingFragment}${overdueFragment}`;
  })();

  // ─── Actions ──────────────────────────────────────────────────────────

  const issue = useCallback(
    async (id: string) => {
      try {
        await billingService.issueInvoice(id);
        toast.success('Invoice issued');
        void loadInvoices();
      } catch {
        toast.error('Failed to issue invoice');
      }
    },
    [loadInvoices],
  );

  const markPaid = useCallback(
    async (id: string) => {
      try {
        await billingService.markPaid(id);
        toast.success('Invoice marked paid');
        void loadInvoices();
      } catch {
        toast.error('Failed to mark invoice as paid');
      }
    },
    [loadInvoices],
  );

  const cancel = useCallback(
    async (id: string) => {
      try {
        await billingService.cancelInvoice(id);
        toast.success('Invoice cancelled');
        void loadInvoices();
      } catch {
        toast.error('Failed to cancel invoice');
      }
    },
    [loadInvoices],
  );

  const rowActions = (invoice: InvoiceResponse): DataTableAction[] => {
    const actions: DataTableAction[] = [
      {
        label: 'View',
        onClick: () => navigate(`/billing/invoices/${invoice.id}`),
      },
    ];
    if (invoice.status === 'Draft') {
      actions.push({ label: 'Issue', onClick: () => void issue(invoice.id) });
    }
    if (invoice.status === 'Issued') {
      actions.push({ label: 'Mark paid', onClick: () => void markPaid(invoice.id) });
    }
    if (invoice.status === 'Draft' || invoice.status === 'Issued') {
      actions.push({
        label: 'Cancel',
        variant: 'danger',
        onClick: () => void cancel(invoice.id),
      });
    }
    return actions;
  };

  // ─── Columns ──────────────────────────────────────────────────────────

  const columns: ColumnDef<InvoiceResponse>[] = [
    {
      id: 'invoice',
      header: 'Invoice',
      accessorKey: 'invoiceNumber',
      sortable: true,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[12px] font-medium text-[var(--color-text-primary)]">
          {shortInvoiceNumber(row.invoiceNumber)}
        </span>
      ),
      className: 'w-[140px] pl-4',
      headerClassName: 'pl-4',
    },
    {
      id: 'party',
      header: 'Counterparty',
      accessorFn: (row) => row.customerName || row.customerPartyId || row.customerId,
      sortable: true,
      cell: (row) => {
        const { label, sub } = partyDisplay(row);
        return (
          <div className="flex items-center gap-2.5">
            <AgentAvatar name={label} size={26} />
            <div className="flex min-w-0 flex-col">
              <span className="truncate text-[13px] font-medium text-[var(--color-text-primary)]">
                {label}
              </span>
              {sub && (
                <span className="font-[family-name:var(--font-mono)] truncate text-[11px] text-[var(--color-text-tertiary)]">
                  {sub}
                </span>
              )}
            </div>
          </div>
        );
      },
    },
    {
      id: 'memo',
      header: 'Memo',
      cell: (row) => {
        const overdue = isOverdue(row);
        const issued = formatDate(row.issuedUtc);
        const due = formatDate(row.dueUtc);
        return (
          <span className="text-[12px] text-[var(--color-text-secondary)]">
            issued {issued} · due {due}
            {overdue && (
              <span className="ml-2 text-[var(--color-danger)]">overdue</span>
            )}
          </span>
        );
      },
      className: 'w-[260px]',
    },
    {
      id: 'date',
      header: 'Date',
      accessorFn: (row) => (row.issuedUtc ? new Date(row.issuedUtc) : null),
      sortable: true,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {formatDate(row.issuedUtc)}
        </span>
      ),
      className: 'w-[110px]',
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (row) => {
        const overdue = isOverdue(row);
        const tone = overdue
          ? STATUS_TONE.Overdue
          : STATUS_TONE[row.status] ?? 'default';
        return (
          <Pill tone={tone} dot>
            {overdue ? 'Overdue' : row.status}
          </Pill>
        );
      },
      className: 'w-[120px]',
    },
    {
      id: 'amount',
      header: 'Amount',
      accessorFn: (row) => row.totalAmount,
      sortable: true,
      cell: (row) => (
        <span className="block text-right font-[family-name:var(--font-mono)] text-[12.5px] font-semibold text-[var(--color-text-primary)]">
          {formatMoney(row.totalAmount, row.currency)}
        </span>
      ),
      className: 'w-[140px] text-right',
      headerClassName: 'text-right',
    },
  ];

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Invoices"
        subtitle={subtitle}
        actions={
          <>
            <Button variant="outline" size="sm" onClick={() => void loadInvoices()} disabled={loading}>
              <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
              Refresh
            </Button>
            <Button size="sm" onClick={() => navigate('/billing/invoices/new')}>
              <Plus className="h-3 w-3" />
              New invoice
            </Button>
          </>
        }
      />

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadInvoices()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={statusFilter}
        onTabChange={setStatusFilter}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter by invoice, customer, currency…"
        hideFilterButton
      />

      <AonikCard padding={0}>
        <DataTable
          data={filtered}
          columns={columns}
          getRowId={(inv) => inv.id}
          showCheckboxes={false}
          loading={loading}
          loadingMessage="Loading invoices…"
          emptyTitle="No invoices found"
          emptyDescription={
            searchQuery || statusFilter
              ? 'Try adjusting the active tab or search.'
              : 'Invoices will appear here as they are issued.'
          }
          rowActions={(inv) => <DataTableRowActions actions={rowActions(inv)} />}
          rowActionsPosition="end"
          onRowClick={(inv) => navigate(`/billing/invoices/${inv.id}`)}
        />
      </AonikCard>
    </div>
  );
}
