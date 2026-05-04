import { useCallback, useEffect, useRef, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, RefreshCw, Search, Shield } from 'lucide-react';
import { roleService } from '@/services/roleService';
import type { AccessRoleSummary, PagedResult } from '@/types';
import { DataTablePagination } from '@/components/ui/data-table';

export function AccessRolesPage() {
  const [roles, setRoles] = useState<AccessRoleSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);
  const requestIdRef = useRef(0);

  const loadRoles = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<AccessRoleSummary> = await roleService.list({
        pageNumber,
        pageSize,
        search: searchQuery || undefined,
      });
      if (requestIdRef.current != requestId)
      {
        return;
      }
      setRoles(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load roles:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      if (requestIdRef.current != requestId)
      {
        return;
      }
      setError(message || 'Failed to load roles. Please try again.');
    } finally {
      if (requestIdRef.current != requestId)
      {
        return;
      }
      setLoading(false);
    }
  }, [pageNumber, pageSize, searchQuery]);

  useEffect(() => {
    loadRoles();
  }, [loadRoles]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery]);

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };
  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Roles</h1>
          <p className="text-[var(--color-text-secondary)]">
            Define reusable permission sets and assign them to teams.
          </p>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadRoles} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="flex items-center justify-between gap-4">
            <div className="relative w-80 max-w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
              <input
                type="text"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Search for roles"
                className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
              />
            </div>

            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={loadRoles}
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
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Role</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Description</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Permissions</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Assigned Users</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={4} className="px-4 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading roles...</p>
                      </td>
                    </tr>
                  ) : roles.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-4 py-12 text-center">
                        <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                          <Shield className="w-12 h-12" />
                        </div>
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No roles found</p>
                        <p className="text-sm text-[var(--color-text-secondary)]">
                          {searchQuery ? 'Try adjusting your search.' : 'Create a role to start assigning permissions.'}
                        </p>
                      </td>
                    </tr>
                  ) : (
                    roles.map((role) => (
                      <tr
                        key={role.roleId}
                        className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)] transition-colors"
                      >
                        <td className="px-4 py-3">
                          <p className="font-medium text-[var(--color-text-primary)]">{role.name}</p>
                        </td>
                        <td className="px-4 py-3">
                          <p className="text-sm text-[var(--color-text-secondary)]">
                            {role.description || 'No description provided.'}
                          </p>
                        </td>
                        <td className="px-4 py-3">
                          <Badge variant="team" className="text-xs">
                            {role.permissionCount} permission{role.permissionCount === 1 ? '' : 's'}
                          </Badge>
                        </td>
                        <td className="px-4 py-3">
                          <Badge variant="outline" className="text-xs">
                            {role.userCount} user{role.userCount === 1 ? '' : 's'}
                          </Badge>
                        </td>
                      </tr>
                    ))
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
