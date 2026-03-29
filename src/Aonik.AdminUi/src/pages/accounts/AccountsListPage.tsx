import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';

import { CreateAccountDialog } from './CreateAccountDialog';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  AlertCircle,
  CheckCircle2,
  Eye,
  Landmark,
} from 'lucide-react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

import { accountService } from '@/services/accountService';
import type { AccountResponse } from '@/types';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
  type FilterOption,
} from '@/components/ui/data-table';
import { PlaidLinkButton } from '@/components/plaid/PlaidLinkButton';

const statusFilterOptions: FilterOption[] = [
  { value: 'Verified', label: 'Verified (Linked)' },
  { value: 'Manual', label: 'Manual' },
];

export function AccountsListPage() {
  const navigate = useNavigate();
  const [accounts, setAccounts] = useState<AccountResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [currencyFilter, setCurrencyFilter] = useState('');
  const [countryFilter, setCountryFilter] = useState('');
  const [showCreateAccount, setShowCreateAccount] = useState(false);

  const loadAccounts = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await accountService.listAccounts();
      setAccounts(result);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load accounts. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAccounts();
  }, [loadAccounts]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter, currencyFilter, countryFilter]);

  // Derive unique currency and country values for filter dropdowns
  const currencyOptions: FilterOption[] = [...new Set(accounts.map((a) => a.currency).filter(Boolean) as string[])]
    .sort()
    .map((c) => ({ value: c, label: c }));

  const countryOptions: FilterOption[] = [...new Set(accounts.map((a) => a.country).filter(Boolean) as string[])]
    .sort()
    .map((c) => ({ value: c, label: c }));

  const filteredAccounts = accounts.filter((a) => {
    if (statusFilter && a.verificationStatus !== statusFilter) return false;
    if (currencyFilter && a.currency !== currencyFilter) return false;
    if (countryFilter && a.country !== countryFilter) return false;
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      return (
        a.maskedIdentifier.toLowerCase().includes(query) ||
        a.accountType.toLowerCase().includes(query) ||
        (a.providerRef ?? '').toLowerCase().includes(query) ||
        (a.currency ?? '').toLowerCase().includes(query) ||
        (a.country ?? '').toLowerCase().includes(query)
      );
    }
    return true;
  });

  const totalCount = filteredAccounts.length;
  const paginatedAccounts = filteredAccounts.slice(
    (pageNumber - 1) * pageSize,
    pageNumber * pageSize
  );

  const getRowActions = (account: AccountResponse): DataTableAction[] => [
    {
      label: 'View Transactions',
      icon: <Eye className="w-4 h-4" />,
      onClick: () => navigate(`/accounts/${account.accountId}/transactions`),
    },
  ];

  const columns: ColumnDef<AccountResponse>[] = [
    {
      id: 'maskedIdentifier',
      header: 'Account',
      accessorKey: 'maskedIdentifier',
      sortable: true,
      cell: (account) => (
        <button
          className="text-left hover:underline"
          onClick={() => navigate(`/accounts/${account.accountId}/transactions`)}
        >
          <p className="font-medium text-[var(--color-text-primary)]">{account.maskedIdentifier}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{account.accountId.slice(0, 8)}...</p>
        </button>
      ),
    },
    {
      id: 'accountType',
      header: 'Type',
      accessorKey: 'accountType',
      sortable: true,
      cell: (account) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{account.accountType}</span>
      ),
    },
    {
      id: 'verificationStatus',
      header: 'Source',
      accessorKey: 'verificationStatus',
      sortable: true,
      cell: (account) => {
        const isLinked = account.verificationStatus === 'Verified';
        const style = isLinked
          ? 'bg-[var(--color-success-light)] text-[var(--color-success)]'
          : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]';
        const label = isLinked ? 'Linked' : 'Manual';
        return (
          <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style}`}>
            {label}
          </span>
        );
      },
    },
    {
      id: 'currency',
      header: 'Currency',
      accessorFn: (row) => row.currency ?? '',
      sortable: true,
      cell: (account) => (
        <span className="text-sm font-medium text-[var(--color-text-primary)]">{account.currency || '—'}</span>
      ),
    },
    {
      id: 'country',
      header: 'Country',
      accessorFn: (row) => row.country ?? '',
      sortable: true,
      cell: (account) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{account.country || '—'}</span>
      ),
    },
    {
      id: 'createdAt',
      header: 'Created',
      accessorFn: (row) => new Date(row.createdAt),
      sortable: true,
      cell: (account) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {new Date(account.createdAt).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })}
        </span>
      ),
    },
  ];

  const breadcrumbItems = [
    { label: 'Accounts', icon: <Landmark className="w-3.5 h-3.5" /> },
  ];

  const totalAccounts = accounts.length;
  const linkedAccounts = accounts.filter((a) => a.verificationStatus === 'Verified').length;
  const manualAccounts = accounts.filter((a) => a.verificationStatus === 'Manual').length;

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Accounts</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage accounts for this tenant.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setShowCreateAccount(true)} className="rounded-sm">
            Add Account
          </Button>
          <PlaidLinkButton
            onSuccess={() => {
              toast.success('Bank account linked successfully.');
              loadAccounts();
            }}
            onError={(msg) => toast.error(msg)}
            className="rounded-sm"
          />
        </div>
      </div>

      <div className="grid gap-4 mb-6 md:grid-cols-3">
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Landmark className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Total Accounts</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{totalAccounts}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">All accounts</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-success-light)] text-[var(--color-success)]">
              <CheckCircle2 className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Linked</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{linkedAccounts}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Via Plaid or provider</p>
            </div>
          </CardContent>
        </Card>
        <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <CardContent className="p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]">
              <Landmark className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Manual</p>
              <p className="text-2xl font-semibold text-[var(--color-text-primary)]">{manualAccounts}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">Manually added</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadAccounts} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="flex items-center gap-3 flex-wrap">
            <DataTableHeader
              searchValue={searchQuery}
              onSearchChange={setSearchQuery}
              searchPlaceholder="Search accounts"
              filterValue={statusFilter}
              onFilterChange={setStatusFilter}
              filterOptions={statusFilterOptions}
              filterPlaceholder="Source"
              className="px-0 border-b-0 flex-1 min-w-0"
            />
            {currencyOptions.length > 0 && (
              <Select value={currencyFilter} onValueChange={(v) => setCurrencyFilter(v === '__all__' ? '' : v)}>
                <SelectTrigger className="w-[120px]">
                  <SelectValue placeholder="Currency" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All currencies</SelectItem>
                  {currencyOptions.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
            {countryOptions.length > 0 && (
              <Select value={countryFilter} onValueChange={(v) => setCountryFilter(v === '__all__' ? '' : v)}>
                <SelectTrigger className="w-[120px]">
                  <SelectValue placeholder="Country" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All countries</SelectItem>
                  {countryOptions.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <DataTable
              data={paginatedAccounts}
              columns={columns}
              getRowId={(a) => a.accountId}
              loading={loading}
              loadingMessage="Loading accounts..."
              emptyIcon={<Landmark className="w-12 h-12" />}
              emptyTitle="No accounts yet"
              emptyDescription={
                searchQuery || statusFilter
                  ? 'Try adjusting your filters.'
                  : 'Add an account manually or link one via Plaid to get started.'
              }
              rowActions={(account) => <DataTableRowActions actions={getRowActions(account)} />}
              rowActionsPosition="start"
            />
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={(s) => { setPageSize(s); setPageNumber(1); }}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>

      <CreateAccountDialog
        open={showCreateAccount}
        onOpenChange={setShowCreateAccount}
        onSuccess={loadAccounts}
      />
    </div>
  );
}
