import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';

import {
  AlertCircle,
  ArrowUpRight,
  Building2,
  Cable,
  Globe2,
  Network,
  Plus,
  Route,
  Trash2,
  UsersRound,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { CountrySelect } from '@/components/ui/country-select';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
  type FilterOption,
} from '@/components/ui/data-table';
import { CreatePartnerDialog } from '@/components/dialogs/CreatePartnerDialog';

import { partnerService } from '@/services/partnerService';
import type { PagedResult } from '@/types';
import type { CreatePartnerRequest, PartnerListItem } from '@/types/partners';

const statusFilterOptions: FilterOption[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Inactive', label: 'Inactive' },
];

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: {
    text: 'text-[var(--color-success)]',
    bg: 'bg-[var(--color-success-light)]',
  },
  Pending: {
    text: 'text-[var(--color-warning)]',
    bg: 'bg-[var(--color-warning-light)]',
  },
  Suspended: {
    text: 'text-[var(--color-error)]',
    bg: 'bg-[var(--color-error-light)]',
  },
  Inactive: {
    text: 'text-[var(--color-text-tertiary)]',
    bg: 'bg-[var(--color-surface-inset)]',
  },
};

const formatDate = (value?: string | null) => {
  if (!value) {
    return '—';
  }

  return new Date(value).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const formatCountryList = (countries: string[]) => {
  if (countries.length === 0) {
    return 'No market coverage';
  }

  if (countries.length <= 3) {
    return countries.join(', ');
  }

  return `${countries.slice(0, 3).join(', ')} +${countries.length - 3}`;
};

export function CatalogPartnersPage() {
  const navigate = useNavigate();

  const [partners, setPartners] = useState<PartnerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [countryFilter, setCountryFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);

  const requestIdRef = useRef(0);

  const loadPartners = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;

    setLoading(true);
    setError(null);

    try {
      const result: PagedResult<PartnerListItem> = await partnerService.list({
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        countryCode: countryFilter || undefined,
        search: searchQuery || undefined,
      });

      if (requestIdRef.current !== requestId) {
        return;
      }

      setPartners(result.items);
      setTotalCount(result.totalCount);
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) {
        return;
      }

      console.error('Failed to load partners:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load partners. Please try again.');
      setLoading(false);
    }
  }, [countryFilter, pageNumber, pageSize, searchQuery, statusFilter]);

  useEffect(() => {
    loadPartners();
  }, [loadPartners]);

  useEffect(() => {
    setPageNumber(1);
  }, [countryFilter, searchQuery, statusFilter]);

  const handleCreatePartner = useCallback(
    async (request: CreatePartnerRequest) => {
      await partnerService.create(request);
      toast.success('Partner created.');
      await loadPartners();
    },
    [loadPartners]
  );

  const handleDeletePartner = useCallback(
    async (partner: PartnerListItem) => {
      const confirmed = confirm(`Delete ${partner.name}? This action cannot be undone.`);
      if (!confirmed) {
        return;
      }

      try {
        await partnerService.delete(partner.partnerId);
        toast.success('Partner deleted.');
        await loadPartners();
      } catch (err: unknown) {
        console.error('Failed to delete partner:', err);
        const message =
          err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '';
        toast.error(message || 'Failed to delete partner.');
      }
    },
    [loadPartners]
  );

  const handlePageSizeChange = (nextPageSize: number) => {
    setPageSize(nextPageSize);
    setPageNumber(1);
  };

  const getRowActions = (partner: PartnerListItem): DataTableAction[] => [
    {
      label: 'View details',
      icon: <ArrowUpRight className="w-4 h-4" />,
      onClick: () => navigate(`/catalog/partners/${partner.partnerId}`),
    },
    {
      label: 'Delete partner',
      icon: <Trash2 className="w-4 h-4" />,
      variant: 'danger',
      onClick: () => {
        void handleDeletePartner(partner);
      },
    },
  ];

  const columns: ColumnDef<PartnerListItem>[] = [
    {
      id: 'partner',
      header: 'Partner',
      accessorKey: 'name',
      sortable: true,
      cell: (partner) => (
        <div>
          <p className="font-medium text-[var(--color-text-primary)]">{partner.name}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{partner.partnerId.slice(0, 8)}</p>
        </div>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (partner) => {
        const style = statusStyles[partner.status] ?? {
          text: 'text-[var(--color-text-secondary)]',
          bg: 'bg-[var(--color-surface-inset)]',
        };

        return (
          <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${style.bg} ${style.text}`}>
            {partner.status}
          </span>
        );
      },
    },
    {
      id: 'coverage',
      header: 'Coverage',
      accessorFn: (row) => row.coverageCountries.join(','),
      sortable: true,
      cell: (partner) => (
        <div>
          <p className="text-sm text-[var(--color-text-primary)]">{formatCountryList(partner.coverageCountries ?? [])}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{partner.branchCount ?? 0} branches</p>
        </div>
      ),
    },
    {
      id: 'connectivity',
      header: 'Connectivity',
      accessorFn: (row) => row.connectorCount,
      sortable: true,
      cell: (partner) => (
        <div className="space-y-1 text-sm">
          <p className="text-[var(--color-text-primary)]">{partner.connectorCount ?? 0} connectors</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{partner.activeRoutingRuleCount ?? 0} active rules</p>
        </div>
      ),
    },
    {
      id: 'billers',
      header: 'Linked billers',
      accessorFn: (row) => row.linkedBillerCount,
      sortable: true,
      cell: (partner) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{partner.linkedBillerCount ?? 0}</span>
      ),
    },
    {
      id: 'updatedAt',
      header: 'Updated',
      accessorFn: (row) => row.updatedAt ? new Date(row.updatedAt) : new Date(row.createdAt),
      sortable: true,
      cell: (partner) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{formatDate(partner.updatedAt ?? partner.createdAt)}</span>
      ),
    },
  ];

  const totalPartners = totalCount;
  const activePartners = partners.filter((partner) => partner.status === 'Active').length;
  const coveredCountriesCount = useMemo(() => {
    const countries = new Set(partners.flatMap((partner) => partner.coverageCountries ?? []));
    return countries.size;
  }, [partners]);
  const connectorsOnPage = partners.reduce((total, partner) => total + (partner.connectorCount ?? 0), 0);

  const breadcrumbItems = [
    { label: 'Catalog', href: '/catalog' },
    { label: 'Partners', icon: <Network className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Partners</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage payout and bill payment partners used for corridor coverage and routing.
          </p>
        </div>
        <Button onClick={() => setIsCreateDialogOpen(true)} className="rounded-sm">
          <Plus className="mr-2 h-4 w-4" />
          New Partner
        </Button>
      </div>

      <div className="mb-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Total partners</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{totalPartners}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Matches current filters</p>
            </div>
          </CardContent>
        </Card>

        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[var(--color-success-light)] text-[var(--color-success)]">
              <UsersRound className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Active partners</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{activePartners}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">On this page</p>
            </div>
          </CardContent>
        </Card>

        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
              <Globe2 className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Markets covered</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{coveredCountriesCount}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Distinct countries on page</p>
            </div>
          </CardContent>
        </Card>

        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="flex items-center gap-3 p-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[var(--color-info-light)] text-[var(--color-info)]">
              <Cable className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Connectors</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{connectorsOnPage}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Across visible partners</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
            <AlertCircle className="h-5 w-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadPartners}>
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <DataTableHeader
            searchValue={searchQuery}
            onSearchChange={setSearchQuery}
            searchPlaceholder="Search partners"
            filterValue={statusFilter}
            onFilterChange={setStatusFilter}
            filterOptions={statusFilterOptions}
            filterPlaceholder="Status"
            showViewToggle={false}
            actions={
              <div className="w-56 max-w-full">
                <CountrySelect
                  value={countryFilter}
                  onChange={setCountryFilter}
                  placeholder="Filter by country"
                  includeEmpty={true}
                  emptyLabel="All countries"
                  className="w-full"
                />
              </div>
            }
            className="border-b-0 px-0"
          />

          <div className="mt-3 overflow-hidden rounded-md border border-[var(--color-border-light)]">
            <DataTable
              data={partners}
              columns={columns}
              getRowId={(partner) => partner.partnerId}
              selectedIds={selectedIds}
              onSelectionChange={setSelectedIds}
              showCheckboxes={true}
              loading={loading}
              loadingMessage="Loading partners..."
              emptyIcon={<Network className="h-12 w-12" />}
              emptyTitle="No partners found"
              emptyDescription={
                searchQuery || statusFilter || countryFilter
                  ? 'Try adjusting your filters.'
                  : 'Add your first partner to unlock destination markets and routing options.'
              }
              rowActions={(partner) => <DataTableRowActions actions={getRowActions(partner)} />}
              rowIcon={(partner) => {
                const style = statusStyles[partner.status] ?? {
                  text: 'text-[var(--color-text-tertiary)]',
                  bg: 'bg-[var(--color-surface-inset)]',
                };

                return (
                  <div className={`flex h-6 w-6 items-center justify-center rounded-full ${style.bg}`}>
                    <Route className={`h-3.5 w-3.5 ${style.text}`} />
                  </div>
                );
              }}
              rowActionsPosition="start"
            />
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={handlePageSizeChange}
              className="border-t-0 px-0"
            />
          </div>

          {selectedIds.size > 0 && (
            <div className="mt-3 flex items-center gap-2">
              <Badge variant="outline">{selectedIds.size} selected</Badge>
            </div>
          )}
        </CardContent>
      </Card>

      <CreatePartnerDialog
        open={isCreateDialogOpen}
        onOpenChange={setIsCreateDialogOpen}
        onSave={handleCreatePartner}
      />
    </div>
  );
}
