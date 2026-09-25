import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
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
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
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
  Active: { icon: CheckCircle, color: 'text-success', bgColor: 'bg-success-subtle' },
  Provisioning: { icon: Clock, color: 'text-warning', bgColor: 'bg-warning-subtle' },
  Deactivated: { icon: XCircle, color: 'text-muted-foreground', bgColor: 'bg-muted' },
  Suspended: { icon: AlertCircle, color: 'text-destructive', bgColor: 'bg-destructive/10' },
};

const environmentColors: Record<string, string> = {
  Dev: 'bg-info-subtle text-info',
  Test: 'bg-agent/10 text-agent',
  Staging: 'bg-warning-subtle text-warning',
  Prod: 'bg-success-subtle text-success',
};

export function TenantsListPage() {
  const navigate = useNavigate();
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
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
      setInitialLoad(false);
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

  if (initialLoad) {
    return <PageLoadingScreen message="Loading tenants" />;
  }

  return (
    <div className="h-full overflow-auto p-6">

      {/* Page Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Tenants</h1>
          <p className="text-muted-foreground">
            Manage all tenants in the platform. Create, configure, and monitor tenant environments.
          </p>
        </div>
        <Button onClick={() => navigate('/tenants/new')}>
          <Plus className="w-4 h-4 mr-2" />
          Create Tenant
        </Button>
      </div>

      {/* Error State */}
      {error && (
        <Card className="mb-6 border-destructive bg-destructive/10">
          <CardContent className="p-4 flex items-center gap-3 text-destructive">
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
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search for tenants"
                  className="pl-10"
                />
              </div>

              <Select
                value={statusFilter || undefined}
                onValueChange={(value) => setStatusFilter(value === '__all__' ? '' : value)}
              >
                <SelectTrigger aria-label="Filter by status" className="h-9">
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
                <SelectTrigger aria-label="Filter by environment" className="h-9">
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
              aria-label="Refresh"
              disabled={loading}
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </Button>
          </div>

          <div className="mt-3 rounded-md border border-border overflow-hidden">
            <Table>
            <TableHeader>
              <TableRow className="bg-muted/50 hover:bg-muted/50">
                <TableHead className="px-4 text-xs text-muted-foreground">Tenant</TableHead>
                <TableHead className="px-4 text-xs text-muted-foreground">Environment</TableHead>
                <TableHead className="px-4 text-xs text-muted-foreground">Status</TableHead>
                <TableHead className="px-4 text-xs text-muted-foreground">Currency</TableHead>
                <TableHead className="px-4 text-xs text-muted-foreground">Created</TableHead>
                <TableHead className="px-4 text-right text-xs text-muted-foreground">Actions</TableHead>
              </TableRow>
            </TableHeader>
                <TableBody>
                  {loading ? (
                    <TableRow className="hover:bg-transparent">
                      <TableCell colSpan={6} className="px-4 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-muted-foreground" />
                        <p className="text-sm text-muted-foreground">Loading tenants...</p>
                      </TableCell>
                    </TableRow>
                  ) : tenants.length === 0 ? (
                    <TableRow className="hover:bg-transparent">
                      <TableCell colSpan={6} className="px-4 py-12 text-center">
                        <div className="mb-3 flex justify-center text-muted-foreground">
                          <Building2 className="w-12 h-12" />
                        </div>
                        <p className="text-foreground font-medium mb-1">No tenants found</p>
                        <p className="text-sm text-muted-foreground mb-4">
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
                      </TableCell>
                    </TableRow>
                  ) : (
                    tenants.map((tenant) => {
                      const StatusIcon = statusConfig[tenant.status]?.icon || AlertCircle;
                      const statusColor = statusConfig[tenant.status]?.color || 'text-muted-foreground';
                      const statusBgColor = statusConfig[tenant.status]?.bgColor || 'bg-muted';
                      const envColor = environmentColors[tenant.environment] || 'bg-muted text-foreground';

                      return (
                        <TableRow
                          key={tenant.tenantId}
                          className="cursor-pointer"
                          onClick={() => navigate(`/tenants/${tenant.tenantId}`)}
                        >
                          <TableCell className="px-4 py-3">
                            <div className="flex items-center gap-3">
                              <div className="w-10 h-10 rounded-md bg-primary/10 flex items-center justify-center">
                                <Building2 className="w-5 h-5 text-primary" />
                              </div>
                              <div>
                                <p className="font-medium text-foreground">{tenant.name}</p>
                                <p className="text-xs text-muted-foreground font-mono">
                                  {tenant.tenantId.substring(0, 8)}...
                                </p>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell className="px-4 py-3">
                            <Badge className={`${envColor} font-medium`}>
                              {tenant.environment}
                            </Badge>
                          </TableCell>
                          <TableCell className="px-4 py-3">
                            <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${statusBgColor} ${statusColor}`}>
                              <StatusIcon className="w-3.5 h-3.5" />
                              {tenant.status}
                            </div>
                          </TableCell>
                          <TableCell className="px-4 py-3">
                            <span className="font-mono text-sm text-foreground">{tenant.defaultCurrency}</span>
                          </TableCell>
                          <TableCell className="px-4 py-3">
                            <span className="text-sm text-muted-foreground">
                              {formatDate(tenant.createdAt)}
                            </span>
                          </TableCell>
                          <TableCell className="px-4 py-3 text-right">
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
                          </TableCell>
                        </TableRow>
                      );
                    })
                  )}
                </TableBody>
            </Table>
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
