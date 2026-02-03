import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { 
  Plus, 
  Search, 
  RefreshCw, 
  Building2, 
  AlertCircle,
  CheckCircle,
  Clock,
  XCircle,
} from 'lucide-react';
import { tenantService } from '@/services/tenantService';
import type { Tenant, TenantStatus, PagedResult } from '@/types';
import { DataTablePagination } from '@/components/ui/data-table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

const statusConfig: Record<TenantStatus, { icon: React.ElementType; color: string; bgColor: string }> = {
  Active: { icon: CheckCircle, color: 'text-[var(--color-success)]', bgColor: 'bg-[var(--color-success-light)]' },
  Provisioning: { icon: Clock, color: 'text-[var(--color-warning)]', bgColor: 'bg-[var(--color-warning-light)]' },
  Deactivated: { icon: XCircle, color: 'text-[var(--color-text-tertiary)]', bgColor: 'bg-[var(--color-surface-inset)]' },
  Suspended: { icon: AlertCircle, color: 'text-[var(--color-error)]', bgColor: 'bg-[var(--color-error-light)]' },
};

const environmentColors: Record<string, string> = {
  Dev: 'bg-[var(--color-info-light)] text-[var(--color-info)]',
  Test: 'bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]',
  Staging: 'bg-[var(--color-pending-light)] text-[var(--color-pending)]',
  Prod: 'bg-[var(--color-success-light)] text-[var(--color-success)]',
};

export function TenantsListPage() {
  const navigate = useNavigate();
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [environmentFilter, setEnvironmentFilter] = useState<string>('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);

  const loadTenants = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<Tenant> = await tenantService.list({
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        environment: environmentFilter || undefined,
        nameFilter: searchQuery || undefined,
      });
      setTenants(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load tenants:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load tenants. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, statusFilter, environmentFilter, searchQuery]);

  useEffect(() => {
    loadTenants();
  }, [loadTenants]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter, environmentFilter]);

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };

  const breadcrumbItems = [
    { label: 'Tenants', icon: <Building2 className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      {/* Page Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Tenants</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage all tenants in the platform. Create, configure, and monitor tenant environments.
          </p>
        </div>
        <Button onClick={() => navigate('/tenants/new')} className="rounded-sm">
          <Plus className="w-4 h-4 mr-2" />
          Create Tenant
        </Button>
      </div>

      {/* Error State */}
      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadTenants} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Tenants Table */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-4 flex-1">
              <div className="relative w-72 max-w-full">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search for tenants"
                  className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
                />
              </div>

              <Select
                value={statusFilter || undefined}
                onValueChange={(value) => setStatusFilter(value === '__all__' ? '' : value)}
              >
                <SelectTrigger aria-label="Filter by status" className="h-9 rounded-sm">
                  <SelectValue placeholder="Filter by status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">Filter by status</SelectItem>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Provisioning">Provisioning</SelectItem>
                  <SelectItem value="Deactivated">Deactivated</SelectItem>
                  <SelectItem value="Suspended">Suspended</SelectItem>
                </SelectContent>
              </Select>

              <Select
                value={environmentFilter || undefined}
                onValueChange={(value) => setEnvironmentFilter(value === '__all__' ? '' : value)}
              >
                <SelectTrigger aria-label="Filter by environment" className="h-9 rounded-sm">
                  <SelectValue placeholder="Filter by environment" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">Filter by environment</SelectItem>
                  <SelectItem value="Dev">Development</SelectItem>
                  <SelectItem value="Test">Test</SelectItem>
                  <SelectItem value="Staging">Staging</SelectItem>
                  <SelectItem value="Prod">Production</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <Button
              variant="ghost"
              size="icon-sm"
              onClick={loadTenants}
              title="Refresh"
              disabled={loading}
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </Button>
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50">
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Tenant
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Environment
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Status
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Currency
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Created
                    </th>
                    <th className="text-right px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading tenants...</p>
                      </td>
                    </tr>
                  ) : tenants.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-12 text-center">
                        <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                          <Building2 className="w-12 h-12" />
                        </div>
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No tenants found</p>
                        <p className="text-sm text-[var(--color-text-secondary)] mb-4">
                          {searchQuery || statusFilter || environmentFilter
                            ? 'Try adjusting your filters'
                            : 'Get started by creating your first tenant'}
                        </p>
                        {!searchQuery && !statusFilter && !environmentFilter && (
                          <Button onClick={() => navigate('/tenants/new')} className="rounded-sm">
                            <Plus className="w-4 h-4 mr-2" />
                            Create Tenant
                          </Button>
                        )}
                      </td>
                    </tr>
                  ) : (
                    tenants.map((tenant) => {
                      const StatusIcon = statusConfig[tenant.status]?.icon || AlertCircle;
                      const statusColor = statusConfig[tenant.status]?.color || 'text-gray-500';
                      const statusBgColor = statusConfig[tenant.status]?.bgColor || 'bg-gray-100';
                      const envColor = environmentColors[tenant.environment] || 'bg-gray-100 text-gray-700';

                      return (
                        <tr
                          key={tenant.tenantId}
                          className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)] cursor-pointer transition-colors"
                          onClick={() => navigate(`/tenants/${tenant.tenantId}`)}
                        >
                          <td className="px-4 py-3">
                            <div className="flex items-center gap-3">
                              <div className="w-10 h-10 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
                                <Building2 className="w-5 h-5 text-[var(--color-brand-primary)]" />
                              </div>
                              <div>
                                <p className="font-medium text-[var(--color-text-primary)]">{tenant.name}</p>
                                <p className="text-xs text-[var(--color-text-tertiary)] font-mono">
                                  {tenant.tenantId.substring(0, 8)}...
                                </p>
                              </div>
                            </div>
                          </td>
                          <td className="px-4 py-3">
                            <Badge className={`${envColor} font-medium`}>
                              {tenant.environment}
                            </Badge>
                          </td>
                          <td className="px-4 py-3">
                            <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${statusBgColor} ${statusColor}`}>
                              <StatusIcon className="w-3.5 h-3.5" />
                              {tenant.status}
                            </div>
                          </td>
                          <td className="px-4 py-3">
                            <span className="text-sm text-[var(--color-text-primary)]">{tenant.defaultCurrency}</span>
                          </td>
                          <td className="px-4 py-3">
                            <span className="text-sm text-[var(--color-text-secondary)]">
                              {formatDate(tenant.createdAt)}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-right">
                            <Button
                              variant="ghost"
                              size="sm"
                              className="rounded-sm"
                              onClick={(e) => {
                                e.stopPropagation();
                                navigate(`/tenants/${tenant.tenantId}`);
                              }}
                            >
                              View
                            </Button>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

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
    </div>
  );
}
