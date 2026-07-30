// Customers — 1:1 visual port of
// templates/aonik-admin-starterkit/screens/customers-orders.jsx (Customers
// half), wired to the existing /admin/customers list endpoint.
//
// Columns are constrained to fields the backend currently returns
// (CustomerListItem). The template's Country / Orders / Total spend / Owner
// columns are left out until the backend exposes them; the row layout still
// matches the template so the page reads as the same screen.

import { useCallback, useEffect, useRef, useState } from 'react';
import type { ChangeEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Download, Plus, RefreshCw, Upload } from 'lucide-react';

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
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { CreateCustomerDialog } from '@/components/dialogs/CreateCustomerDialog';
import { customerService } from '@/services/customerService';
import type { CustomerDataImportResponse } from '@/services/customerService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type {
  CreateCustomerRequest,
  CustomerListItem,
  CustomerRegistryCurrencyTotal,
  PagedResult,
} from '@/types';
import { DomainChips } from './components/DomainChips';
import { presentDomain } from './components/domainPresentation';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

/**
 * Render a Party GUID as a stable "CUS-XXXXXXXX" handle. The first 8 hex
 * chars are sufficient to disambiguate within a tenant and matches the
 * template's mono-spaced ID column.
 */
function formatCustomerId(partyId: string): string {
  const compact = partyId.replace(/-/g, '').slice(0, 8).toUpperCase();
  return `CUS-${compact}`;
}

function getCustomerPhotoUrl(customer: CustomerListItem): string | null {
  const photoUrl = customer.photoUrlTiny;

  if (!photoUrl) return null;
  if (photoUrl.startsWith('http')) return photoUrl;

  const apiBaseUrl = import.meta.env.VITE_API_URL || 'https://localhost:5001';
  return `${apiBaseUrl}${photoUrl}`;
}

const STATUS_TONE: Record<string, PillTone> = {
  Active: 'success',
  Pending: 'warning',
  Suspended: 'danger',
  Deactivated: 'muted',
};

// The KYC tone map moved with its column: verification is compliance content and belongs on
// the customer detail's Finance tab, not on the product-agnostic registry (Spec 080).

// Party type is a shipped server filter. KYC is NOT a registry filter any more —
// compliance is domain content and lives on the customer detail's Finance tab (Spec 080).
const BASE_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Business', label: 'Business' },
  { value: 'Person', label: 'Person' },
];

/** Domain tabs use a prefix so one tab strip can carry both filter kinds unambiguously. */
const DOMAIN_TAB_PREFIX = 'domain:';

/**
 * Lifetime order value, one line per currency. A customer who paid £400 and ₦90,000 shows
 * both — summing them would require an exchange rate the registry has no business choosing
 * (Spec 080). Unknown/malformed codes fall back to `CODE amount` rather than throwing.
 */
function RegistryTotalValue({ totals }: { totals: CustomerRegistryCurrencyTotal[] }) {
  if (totals.length === 0) {
    return <span className="block text-right text-[var(--color-text-tertiary)]">—</span>;
  }

  return (
    <span className="flex flex-col items-end gap-0.5">
      {totals.map((total) => {
        let text: string;
        try {
          text = new Intl.NumberFormat('en-GB', {
            style: 'currency',
            currency: total.currency,
            currencyDisplay: 'narrowSymbol',
          }).format(total.amount);
        } catch {
          text = `${total.currency} ${total.amount.toFixed(2)}`;
        }
        return (
          <span
            key={total.currency}
            className="font-[family-name:var(--font-mono)] text-xs font-semibold tabular-nums text-[var(--color-text-primary)]"
          >
            {text}
          </span>
        );
      })}
    </span>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────

export function CustomersListPage() {
  const navigate = useNavigate();

  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  // Active tab maps to either a partyType filter ("Business"/"Person") or a
  // status filter ("Pending" — used for "Pending KYC" until the API exposes
  // a verificationStatus query). "" means no filter.
  const [activeTab, setActiveTab] = useState<string>('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);

  // Which product lines have customers at all — the registry only offers tabs that can
  // return rows, and only when the server actually supports the domain filter.
  const [registryDomains, setRegistryDomains] = useState<string[]>([]);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [importResult, setImportResult] = useState<CustomerDataImportResponse | null>(null);
  const importFileRef = useRef<HTMLInputElement>(null);
  const requestIdRef = useRef(0);

  const partyTypeFilter = activeTab === 'Business' || activeTab === 'Person' ? activeTab : undefined;
  const domainFilter = activeTab.startsWith(DOMAIN_TAB_PREFIX)
    ? activeTab.slice(DOMAIN_TAB_PREFIX.length)
    : undefined;

  const loadCustomers = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<CustomerListItem> = await customerService.list({
        pageNumber,
        pageSize,
        partyType: partyTypeFilter,
        domain: domainFilter,
        search: searchQuery || undefined,
      });
      if (requestIdRef.current !== requestId) return;
      setCustomers(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load customers. Please try again.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [pageNumber, pageSize, partyTypeFilter, domainFilter, searchQuery]);

  useEffect(() => {
    void loadCustomers();
  }, [loadCustomers]);

  // Domain tabs are additive: a server without the read-model extension simply serves no
  // domains, and the registry keeps working with the party-type tabs alone.
  useEffect(() => {
    let cancelled = false;
    customerService
      .listDomains()
      .then((result) => {
        if (!cancelled) setRegistryDomains(result.domains ?? []);
      })
      .catch(() => {
        if (!cancelled) setRegistryDomains([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Reset to page 1 whenever a filter changes.
  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, activeTab]);

  const handleCreate = useCallback(
    async (data: CreateCustomerRequest) => {
      await customerService.create(data);
      await loadCustomers();
    },
    [loadCustomers],
  );

  const handleImport = useCallback(
    async (event: ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      if (!file) return;
      setIsImporting(true);
      setError(null);
      setImportResult(null);
      try {
        const result = await customerService.importData(file);
        setImportResult(result);
        await loadCustomers();
      } catch (err: unknown) {
        let msg = 'Import failed';
        if (err && typeof err === 'object' && 'response' in err) {
          const axiosErr = err as {
            response?: { data?: { errors?: { generalErrors?: string[] }; message?: string } };
          };
          const generalErrors = axiosErr.response?.data?.errors?.generalErrors;
          if (generalErrors && generalErrors.length > 0) msg = generalErrors.join('; ');
          else if (axiosErr.response?.data?.message) msg = axiosErr.response.data.message;
        } else if (err instanceof Error) {
          msg = err.message;
        }
        setError(msg);
      } finally {
        setIsImporting(false);
        if (importFileRef.current) importFileRef.current.value = '';
      }
    },
    [loadCustomers],
  );

  // ─── Columns ──────────────────────────────────────────────────────────

  const columns: ColumnDef<CustomerListItem>[] = [
    {
      id: 'id',
      header: 'ID',
      accessorFn: (row) => row.partyId,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-xs font-medium text-[var(--color-text-primary)]">
          {formatCustomerId(row.partyId)}
        </span>
      ),
      // The DataTable's first-column rule defaults to pl-0 (it assumes a
      // leading checkbox or row-icon column will supply the gutter). With
      // both disabled, restore the gutter on the ID column itself.
      className: 'w-[140px] pl-4',
      headerClassName: 'pl-4',
    },
    {
      id: 'name',
      header: 'Customer',
      accessorFn: (row) => row.displayName ?? '',
      sortable: true,
      cell: (row) => {
        const customerName = row.displayName || row.primaryEmail || 'Customer';
        const photoUrl = getCustomerPhotoUrl(row);

        return (
          <div className="flex items-center gap-2.5">
            <Avatar className="h-[26px] w-[26px] rounded-[7px]">
              {photoUrl ? <AvatarImage src={photoUrl} alt={customerName} className="object-cover" /> : null}
              <AvatarFallback className="rounded-[7px] bg-transparent p-0 text-inherit">
                <AgentAvatar name={customerName} size={26} />
              </AvatarFallback>
            </Avatar>
            <div className="flex min-w-0 flex-col">
              <span className="truncate text-[13px] font-medium text-[var(--color-text-primary)]">
                {row.displayName || row.primaryEmail || '—'}
              </span>
              {row.primaryEmail && row.displayName && (
                <span className="truncate text-[11px] text-[var(--color-text-tertiary)]">
                  {row.primaryEmail}
                </span>
              )}
            </div>
          </div>
        );
      },
    },
    {
      id: 'type',
      header: 'Type',
      accessorKey: 'partyType',
      sortable: true,
      cell: (row) => (
        <span className="text-xs text-[var(--color-text-secondary)]">{row.partyType}</span>
      ),
      className: 'w-[100px]',
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (row) => (
        <Pill tone={STATUS_TONE[row.status] ?? 'default'}>{row.status}</Pill>
      ),
      className: 'w-[120px]',
    },
    {
      id: 'createdAt',
      header: 'Created',
      accessorFn: (row) => (row.createdAt ? new Date(row.createdAt) : null),
      sortable: true,
      cell: (row) => (
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {formatDate(row.createdAt)}
        </span>
      ),
      className: 'w-[120px]',
    },
  ];

  // Spec 080 — the read-model columns degrade by ABSENCE, never by placeholder zeros: an
  // older server that serves none of these fields simply gets the base registry. `domains`
  // and `totalValue` legitimately arrive empty, so presence is tested against undefined.
  const supportsRegistryColumns = customers.some(
    (c) =>
      c.country !== undefined ||
      c.domains !== undefined ||
      c.orderCount !== undefined ||
      c.totalValue !== undefined,
  );

  if (supportsRegistryColumns) {
    columns.splice(
      3,
      0,
      {
        id: 'country',
        header: 'Country',
        accessorFn: (row) => row.country ?? '',
        cell: (row) => (
          <span className="text-xs text-[var(--color-text-secondary)]">{row.country || '—'}</span>
        ),
        className: 'w-[100px]',
      },
      {
        id: 'domains',
        header: 'Products',
        accessorFn: (row) => (row.domains ?? []).join(','),
        cell: (row) => <DomainChips domains={row.domains ?? []} />,
        className: 'w-[210px]',
      },
      {
        id: 'orderCount',
        header: 'Orders',
        accessorFn: (row) => row.orderCount ?? 0,
        sortable: true,
        cell: (row) => (
          <span className="block text-right font-[family-name:var(--font-mono)] text-xs tabular-nums text-[var(--color-text-secondary)]">
            {(row.orderCount ?? 0).toLocaleString()}
          </span>
        ),
        className: 'w-[90px] text-right',
        headerClassName: 'text-right',
      },
      {
        id: 'totalValue',
        header: 'Total value',
        // Sorting by a cross-currency figure would rank on an invented exchange rate, so
        // this column is deliberately not sortable.
        accessorFn: (row) => (row.totalValue ?? []).length,
        cell: (row) => <RegistryTotalValue totals={row.totalValue ?? []} />,
        className: 'w-[150px] text-right',
        headerClassName: 'text-right',
      },
    );
  }

  const tabs: FilterBarTab[] = [
    ...BASE_TABS,
    ...registryDomains.map((domain) => ({
      value: `${DOMAIN_TAB_PREFIX}${domain}`,
      label: presentDomain(domain).label,
    })),
  ];

  const rowActions = (customer: CustomerListItem): DataTableAction[] => [
    {
      label: 'View details',
      onClick: () => navigate(`/customers/${customer.partyId}`),
    },
  ];

  // ─── Header counts ────────────────────────────────────────────────────

  if (initialLoad) {
    return <PageLoadingScreen message="Loading customers" />;
  }

  // One registry for every product line, and the order figures are spine-wide (ADR-011):
  // boxes, bill payments and transfers all count, so the subtitle says so rather than
  // leaving an operator to assume one product line.
  const subtitle = totalCount > 0
    ? `${totalCount.toLocaleString()} total — orders and value count every product line`
    : 'Every person and business this tenant transacts with, across all product lines';

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Transact · Customers"
        title="Customers"
        subtitle={subtitle}
        actions={
          <>
            <input
              ref={importFileRef}
              type="file"
              accept=".json"
              className="hidden"
              onChange={handleImport}
            />
            <Button
              variant="outline"
              size="sm"
              disabled={isImporting}
              onClick={() => importFileRef.current?.click()}
            >
              <Upload className="h-3 w-3" />
              {isImporting ? 'Importing…' : 'Import'}
            </Button>
            <Button variant="outline" size="sm" disabled>
              <Download className="h-3 w-3" />
              Export
            </Button>
            <Button size="sm" onClick={() => setIsCreateOpen(true)}>
              <Plus className="h-3 w-3" />
              New customer
            </Button>
          </>
        }
      />

      {importResult && (
        <div className="rounded-md border border-[var(--color-success)] bg-[var(--color-success-light)] p-3 text-sm">
          <p className="font-medium text-[var(--color-success)]">
            Imported {importResult.totalEntities} entities
          </p>
          <p className="mt-1 text-[var(--color-text-secondary)]">
            New customer ID: <code className="text-xs">{importResult.newPartyId}</code>
          </p>
          {importResult.warnings.length > 0 && (
            <ul className="mt-2 list-disc pl-5 text-[var(--color-warning)]">
              {importResult.warnings.map((w, i) => (
                <li key={i}>{w}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadCustomers()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={tabs}
        active={activeTab}
        onTabChange={setActiveTab}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter by name, email, ID…"
        hideFilterButton
      />

      <AonikCard padding={0}>
        <DataTable
          data={customers}
          columns={columns}
          getRowId={(c) => c.partyId}
          showCheckboxes={false}
          loading={loading}
          loadingMessage="Loading customers…"
          emptyTitle="No customers found"
          emptyDescription={
            searchQuery || activeTab
              ? 'Try adjusting the active tab or search.'
              : 'Customers will appear here as they are created or linked.'
          }
          rowActions={(c) => <DataTableRowActions actions={rowActions(c)} />}
          rowActionsPosition="end"
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

      <CreateCustomerDialog
        open={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        onSave={handleCreate}
      />
    </div>
  );
}
