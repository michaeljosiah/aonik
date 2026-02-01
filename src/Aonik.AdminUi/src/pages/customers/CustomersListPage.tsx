import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { AlertCircle, Building2, Eye, Plus, User, UserCheck, UsersRound, UserX } from 'lucide-react';

import { customerService } from '@/services/customerService';
import type { CreateCustomerRequest, CustomerListItem, PagedResult } from '@/types';
import {
  DataTable,
  DataTableGridView,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
  type FilterOption,
  type ViewMode,
} from '@/components/ui/data-table';
import { CreateCustomerDialog } from '@/components/dialogs/CreateCustomerDialog';

const statusStyles: Record<string, { text: string; bg: string; iconColor: string }> = {
  Active: {
    text: 'text-[var(--color-success)]',
    bg: 'bg-[var(--color-success-light)]',
    iconColor: 'text-[var(--color-brand-primary)]',
  },
  Pending: {
    text: 'text-[var(--color-warning)]',
    bg: 'bg-[var(--color-warning-light)]',
    iconColor: 'text-[var(--color-warning)]',
  },
  Deactivated: {
    text: 'text-[var(--color-text-tertiary)]',
    bg: 'bg-[var(--color-surface-inset)]',
    iconColor: 'text-[var(--color-text-tertiary)]',
  },
  Suspended: {
    text: 'text-[var(--color-error)]',
    bg: 'bg-[var(--color-error-light)]',
    iconColor: 'text-[var(--color-error)]',
  },
};

const statusFilterOptions: FilterOption[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Deactivated', label: 'Deactivated' },
  { value: 'Suspended', label: 'Suspended' },
];

const partyTypeFilterOptions: FilterOption[] = [
  { value: 'Person', label: 'Person' },
  { value: 'Business', label: 'Business' },
];

export function CustomersListPage() {
  const navigate = useNavigate();
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [partyTypeFilter, setPartyTypeFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [viewMode, setViewMode] = useState<ViewMode>('list');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const requestIdRef = useRef(0);
  const [imageLoadStates, setImageLoadStates] = useState<Record<string, 'loading' | 'loaded' | 'error'>>({});
  const [imageErrors, setImageErrors] = useState<Set<string>>(new Set());
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);

  const loadCustomers = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<CustomerListItem> = await customerService.list({
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        partyType: partyTypeFilter || undefined,
        search: searchQuery || undefined,
      });
      if (requestIdRef.current !== requestId) {
        return;
      }
      setCustomers(result.items);
      setTotalCount(result.totalCount);
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) {
        return;
      }
      console.error('Failed to load customers:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load customers. Please try again.');
      setLoading(false);
    }
  }, [pageNumber, pageSize, partyTypeFilter, searchQuery, statusFilter]);

  useEffect(() => {
    loadCustomers();
  }, [loadCustomers]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter, partyTypeFilter]);

  useEffect(() => {
    setImageLoadStates({});
    setImageErrors(new Set());
  }, [customers]);

  const handleImageLoad = useCallback((partyId: string) => {
    setImageLoadStates((prev) => ({ ...prev, [partyId]: 'loaded' }));
  }, []);

  const handleImageError = useCallback((partyId: string) => {
    setImageLoadStates((prev) => ({ ...prev, [partyId]: 'error' }));
    setImageErrors((prev) => new Set([...prev, partyId]));
  }, []);

  const handleCreateCustomer = useCallback(
    async (data: CreateCustomerRequest) => {
      await customerService.create(data);
      await loadCustomers();
    },
    [loadCustomers]
  );

  const getPhotoUrl = (customer: CustomerListItem) => {
    const photoUrl = customer.photoUrlTiny;
    if (!photoUrl) return null;
    if (photoUrl.startsWith('http')) return photoUrl;
    const apiBaseUrl = import.meta.env.VITE_API_URL || 'https://localhost:5001';
    return `${apiBaseUrl}${photoUrl}`;
  };

  const getCustomerInitials = (customer: CustomerListItem) => {
    const base = customer.displayName || customer.primaryEmail || 'Customer';
    return base
      .split(' ')
      .filter(Boolean)
      .map((n) => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  };

  const formatDate = (dateString?: string | null) => {
    if (!dateString) return '';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const getRowActions = (customer: CustomerListItem): DataTableAction[] => [
    {
      label: 'View Details',
      icon: <Eye className="w-4 h-4" />,
      onClick: () => navigate(`/customers/${customer.partyId}`),
    },
  ];

  const renderCustomerIcon = (customer: CustomerListItem) => {
    const style = statusStyles[customer.status] ?? { iconColor: 'text-[var(--color-text-tertiary)]' };
    const photoUrl = getPhotoUrl(customer);
    const initials = getCustomerInitials(customer);
    const hasError = imageErrors.has(customer.partyId);
    const isLoading = imageLoadStates[customer.partyId] === 'loading';

    return (
      <Avatar className="w-6 h-6">
        {photoUrl && !hasError ? (
          <>
            <AvatarImage
              src={photoUrl}
              alt={customer.displayName}
              onLoadStart={() => setImageLoadStates((prev) => ({ ...prev, [customer.partyId]: 'loading' }))}
              onLoad={() => handleImageLoad(customer.partyId)}
              onError={() => handleImageError(customer.partyId)}
              className={isLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
            />
            {isLoading && (
              <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                <div className="w-3 h-3 border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin" />
              </div>
            )}
          </>
        ) : null}
        <AvatarFallback className={`text-xs ${style.iconColor} bg-[var(--color-surface-inset)]`}>
          {initials}
        </AvatarFallback>
      </Avatar>
    );
  };

  const columns: ColumnDef<CustomerListItem>[] = [
    {
      id: 'customer',
      header: 'Customer',
      accessorFn: (row) => row.displayName || row.primaryEmail || '',
      sortable: true,
      cell: (customer) => (
        <div>
          <p className="font-medium text-[var(--color-text-primary)]">{customer.displayName}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">
            {customer.primaryEmail || customer.primaryPhone || '—'}
          </p>
        </div>
      ),
    },
    {
      id: 'type',
      header: 'Type',
      accessorKey: 'partyType',
      sortable: true,
      cell: (customer) => (
        <Badge variant="outline" className="text-xs">
          {customer.partyType}
        </Badge>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (customer) => {
        const style = statusStyles[customer.status] ?? {
          text: 'text-[var(--color-text-secondary)]',
          bg: 'bg-[var(--color-surface-inset)]',
        };
        return (
          <span
            className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}
          >
            {customer.status}
          </span>
        );
      },
    },
    {
      id: 'verification',
      header: 'Verification',
      accessorFn: (row) => row.verificationStatus || '',
      sortable: true,
      cell: (customer) => {
        return <span className="text-sm text-[var(--color-text-secondary)]">{customer.verificationStatus || '—'}</span>;
      },
    },
    {
      id: 'createdAt',
      header: 'Created',
      accessorFn: (row) => row.createdAt ? new Date(row.createdAt) : null,
      sortable: true,
      cell: (customer) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{formatDate(customer.createdAt)}</span>
      ),
    },
  ];

  const renderCustomerCard = (customer: CustomerListItem) => {
    const style = statusStyles[customer.status] ?? {
      text: 'text-[var(--color-text-secondary)]',
      bg: 'bg-[var(--color-surface-inset)]',
      iconColor: 'text-[var(--color-text-tertiary)]',
    };
    const photoUrl = getPhotoUrl(customer);
    const initials = getCustomerInitials(customer);
    const hasError = imageErrors.has(customer.partyId);
    const isLoading = imageLoadStates[customer.partyId] === 'loading';
    const verification = customer.verificationStatus;

    return (
      <div className="space-y-3">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            <Avatar className="w-8 h-8 relative">
              {photoUrl && !hasError ? (
                <>
                  <AvatarImage
                    src={photoUrl}
                    alt={customer.displayName}
                    onLoadStart={() => setImageLoadStates((prev) => ({ ...prev, [customer.partyId]: 'loading' }))}
                    onLoad={() => handleImageLoad(customer.partyId)}
                    onError={() => handleImageError(customer.partyId)}
                    className={isLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
                  />
                  {isLoading && (
                    <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                      <div className="w-4 h-4 border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin" />
                    </div>
                  )}
                </>
              ) : null}
              <AvatarFallback className={`text-sm ${style.iconColor} bg-[var(--color-surface-inset)]`}>
                {initials}
              </AvatarFallback>
            </Avatar>
            <div>
              <p className="font-medium text-[var(--color-text-primary)]">{customer.displayName}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">
                {customer.primaryEmail || customer.primaryPhone || '—'}
              </p>
            </div>
          </div>
          <DataTableRowActions actions={getRowActions(customer)} />
        </div>
        <div className="flex items-center gap-3">
          <span
            className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}
          >
            {customer.status}
          </span>
          <Badge variant="outline" className="text-xs">
            {customer.partyType}
          </Badge>
        </div>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Verification</p>
            <p className="text-sm text-[var(--color-text-primary)]">{verification || '—'}</p>
          </div>
          <div className="text-right">
            <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Created</p>
            <p className="text-sm text-[var(--color-text-primary)]">{formatDate(customer.createdAt)}</p>
          </div>
        </div>
      </div>
    );
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };

  const breadcrumbItems = [
    { label: 'Customers', icon: <User className="w-3.5 h-3.5" /> },
  ];

  const totalCustomers = totalCount;
  const activeCustomers = customers.filter((customer) => customer.status === 'Active').length;
  const pendingCustomers = customers.filter((customer) => customer.status === 'Pending').length;
  const businessCustomers = customers.filter((customer) => customer.partyType === 'Business').length;

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Customers</h1>
          <p className="text-[var(--color-text-secondary)]">
            Browse people and businesses connected to this tenant.
          </p>
        </div>
        <Button onClick={() => setIsCreateDialogOpen(true)} className="rounded-sm">
          <Plus className="w-4 h-4 mr-2" />
          New Customer
        </Button>
      </div>

      <div className="grid gap-4 mb-6 md:grid-cols-2 xl:grid-cols-4">
        <Card className="border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <UsersRound className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Total customers</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{totalCustomers}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Matches current filters</p>
            </div>
          </CardContent>
        </Card>
        <Card className="border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-success-light)] text-[var(--color-success)]">
              <UserCheck className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Active customers</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{activeCustomers}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">On this page</p>
            </div>
          </CardContent>
        </Card>
        <Card className="border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-warning-light)] text-[var(--color-warning)]">
              <UserX className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Pending customers</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{pendingCustomers}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">On this page</p>
            </div>
          </CardContent>
        </Card>
        <Card className="border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Building2 className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Businesses</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{businessCustomers}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">On this page</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadCustomers} className="ml-auto">
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
            searchPlaceholder="Search customers"
            filterValue={statusFilter}
            onFilterChange={setStatusFilter}
            filterOptions={statusFilterOptions}
            filterPlaceholder="Status"
            viewMode={viewMode}
            onViewModeChange={setViewMode}
            showViewToggle={true}
            actions={
              <div className="flex items-center gap-2">
                <div className="relative inline-flex items-center">
                  <select
                    value={partyTypeFilter}
                    onChange={(e) => setPartyTypeFilter(e.target.value)}
                    className="appearance-none h-9 pl-3 pr-9 text-sm rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)] cursor-pointer"
                    aria-label="Party type"
                  >
                    <option value="" className="bg-[var(--color-surface)] text-[var(--color-text-primary)]">
                      Type
                    </option>
                    {partyTypeFilterOptions.map((option) => (
                      <option
                        key={option.value}
                        value={option.value}
                        className="bg-[var(--color-surface)] text-[var(--color-text-primary)]"
                      >
                        {option.label}
                      </option>
                    ))}
                  </select>
                  <span className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)] pointer-events-none">
                    v
                  </span>
                </div>
              </div>
            }
            className="px-0 border-b-0"
          />

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            {viewMode === 'list' ? (
              <DataTable
                data={customers}
                columns={columns}
                getRowId={(c) => c.partyId}
                selectedIds={selectedIds}
                onSelectionChange={setSelectedIds}
                showCheckboxes={true}
                rowIcon={renderCustomerIcon}
                loading={loading}
                loadingMessage="Loading customers..."
                emptyIcon={<UsersRound className="w-12 h-12" />}
                emptyTitle="No customers found"
                emptyDescription={
                  searchQuery || statusFilter || partyTypeFilter
                    ? 'Try adjusting your filters.'
                    : 'Customers will appear here as they are created or linked.'
                }
                rowActions={(customer) => <DataTableRowActions actions={getRowActions(customer)} />}
                rowActionsPosition="start"
              />
            ) : (
              <DataTableGridView
                data={customers}
                getRowId={(c) => c.partyId}
                renderCard={renderCustomerCard}
                selectedIds={selectedIds}
                onSelectionChange={setSelectedIds}
                showCheckboxes={true}
                loading={loading}
                loadingMessage="Loading customers..."
                emptyIcon={<UsersRound className="w-12 h-12" />}
                emptyTitle="No customers found"
                emptyDescription={
                  searchQuery || statusFilter || partyTypeFilter
                    ? 'Try adjusting your filters.'
                    : 'Customers will appear here as they are created or linked.'
                }
                columns={3}
              />
            )}
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={handlePageSizeChange}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>

      <CreateCustomerDialog
        open={isCreateDialogOpen}
        onOpenChange={setIsCreateDialogOpen}
        onSave={handleCreateCustomer}
      />
    </div>
  );
}
