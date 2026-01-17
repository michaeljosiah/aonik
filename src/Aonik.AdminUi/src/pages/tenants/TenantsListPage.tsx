import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { 
  Plus, 
  Search, 
  RefreshCw, 
  Building2, 
  ChevronLeft, 
  ChevronRight,
  AlertCircle,
  CheckCircle,
  Clock,
  XCircle,
} from 'lucide-react';
import { tenantService } from '@/services/tenantService';
import type { Tenant, TenantStatus, PagedResult } from '@/types';

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
  const [pageSize] = useState(10);
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
    } catch (err) {
      console.error('Failed to load tenants:', err);
      setError('Failed to load tenants. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, statusFilter, environmentFilter, searchQuery]);

  useEffect(() => {
    loadTenants();
  }, [loadTenants]);

  const totalPages = Math.ceil(totalCount / pageSize);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPageNumber(1);
    loadTenants();
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Tenants</h1>
            <p className="text-[var(--color-text-secondary)]">
              Manage all tenants in the platform. Create, configure, and monitor tenant environments.
            </p>
          </div>
          <Button onClick={() => navigate('/tenants/new')}>
            <Plus className="w-4 h-4 mr-2" />
            Create Tenant
          </Button>
        </div>

        {/* Filters */}
        <Card className="mb-6">
          <CardContent className="p-4">
            <form onSubmit={handleSearch} className="flex flex-wrap gap-4 items-end">
              {/* Search */}
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Search
                </label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search by name..."
                    className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                  />
                </div>
              </div>

              {/* Status Filter */}
              <div className="w-40">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Status
                </label>
                <select
                  value={statusFilter}
                  onChange={(e) => { setStatusFilter(e.target.value); setPageNumber(1); }}
                  className="w-full px-3 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                >
                  <option value="">All Statuses</option>
                  <option value="Active">Active</option>
                  <option value="Provisioning">Provisioning</option>
                  <option value="Deactivated">Deactivated</option>
                  <option value="Suspended">Suspended</option>
                </select>
              </div>

              {/* Environment Filter */}
              <div className="w-40">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Environment
                </label>
                <select
                  value={environmentFilter}
                  onChange={(e) => { setEnvironmentFilter(e.target.value); setPageNumber(1); }}
                  className="w-full px-3 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                >
                  <option value="">All Environments</option>
                  <option value="Dev">Development</option>
                  <option value="Test">Test</option>
                  <option value="Staging">Staging</option>
                  <option value="Prod">Production</option>
                </select>
              </div>

              {/* Actions */}
              <div className="flex gap-2">
                <Button type="submit" variant="outline">
                  <Search className="w-4 h-4 mr-2" />
                  Search
                </Button>
                <Button type="button" variant="ghost" onClick={loadTenants}>
                  <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

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
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)]">
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">
                      Tenant
                    </th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">
                      Environment
                    </th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">
                      Status
                    </th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">
                      Currency
                    </th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">
                      Created
                    </th>
                    <th className="text-right px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading tenants...</p>
                      </td>
                    </tr>
                  ) : tenants.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center">
                        <Building2 className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No tenants found</p>
                        <p className="text-sm text-[var(--color-text-secondary)] mb-4">
                          {searchQuery || statusFilter || environmentFilter
                            ? 'Try adjusting your filters'
                            : 'Get started by creating your first tenant'}
                        </p>
                        {!searchQuery && !statusFilter && !environmentFilter && (
                          <Button onClick={() => navigate('/tenants/new')}>
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
                          className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-background)] cursor-pointer transition-colors"
                          onClick={() => navigate(`/tenants/${tenant.tenantId}`)}
                        >
                          <td className="px-6 py-4">
                            <div className="flex items-center gap-3">
                              <div className="w-10 h-10 rounded-lg bg-[var(--color-brand-primary-light)] flex items-center justify-center">
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
                          <td className="px-6 py-4">
                            <Badge className={`${envColor} font-medium`}>
                              {tenant.environment}
                            </Badge>
                          </td>
                          <td className="px-6 py-4">
                            <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${statusBgColor} ${statusColor}`}>
                              <StatusIcon className="w-3.5 h-3.5" />
                              {tenant.status}
                            </div>
                          </td>
                          <td className="px-6 py-4">
                            <span className="text-sm text-[var(--color-text-primary)]">{tenant.defaultCurrency}</span>
                          </td>
                          <td className="px-6 py-4">
                            <span className="text-sm text-[var(--color-text-secondary)]">
                              {formatDate(tenant.createdAt)}
                            </span>
                          </td>
                          <td className="px-6 py-4 text-right">
                            <Button
                              variant="ghost"
                              size="sm"
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

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-6 py-4 border-t border-[var(--color-border-light)]">
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Showing {((pageNumber - 1) * pageSize) + 1} to {Math.min(pageNumber * pageSize, totalCount)} of {totalCount} tenants
                </p>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={pageNumber <= 1}
                    onClick={() => setPageNumber(p => p - 1)}
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </Button>
                  <span className="text-sm text-[var(--color-text-primary)] px-2">
                    Page {pageNumber} of {totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={pageNumber >= totalPages}
                    onClick={() => setPageNumber(p => p + 1)}
                  >
                    <ChevronRight className="w-4 h-4" />
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
