import { useCallback, useEffect, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, RefreshCw, Search, Shield, ShieldPlus } from 'lucide-react';
import { roleService } from '@/services/roleService';
import type { AccessRoleSummary, PagedResult } from '@/types';

export function AccessRolesPage() {
  const [roles, setRoles] = useState<AccessRoleSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);

  const loadRoles = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<AccessRoleSummary> = await roleService.list({
        pageNumber,
        pageSize,
        search: searchQuery || undefined,
      });
      setRoles(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load roles:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load roles. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, searchQuery]);

  useEffect(() => {
    loadRoles();
  }, [loadRoles]);

  const totalPages = Math.ceil(totalCount / pageSize);

  const handleSearch = (event: React.FormEvent) => {
    event.preventDefault();
    setPageNumber(1);
    loadRoles();
  };

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Roles</h1>
            <p className="text-[var(--color-text-secondary)]">
              Define reusable permission sets and assign them to teams.
            </p>
          </div>
          <Button disabled title="Role creation coming soon">
            <ShieldPlus className="w-4 h-4 mr-2" />
            Create Role
          </Button>
        </div>

        <Card className="mb-6">
          <CardContent className="p-4">
            <form onSubmit={handleSearch} className="flex flex-wrap gap-4 items-end">
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Search
                </label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(event) => setSearchQuery(event.target.value)}
                    placeholder="Search by role name..."
                    className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                  />
                </div>
              </div>
              <div className="flex gap-2">
                <Button type="submit" variant="outline">
                  <Search className="w-4 h-4 mr-2" />
                  Search
                </Button>
                <Button type="button" variant="ghost" onClick={loadRoles}>
                  <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

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
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)]">
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Role</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Description</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Permissions</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Assigned Users</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={4} className="px-6 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading roles...</p>
                      </td>
                    </tr>
                  ) : roles.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-6 py-12 text-center">
                        <Shield className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No roles found</p>
                        <p className="text-sm text-[var(--color-text-secondary)]">
                          {searchQuery ? 'Try adjusting your search.' : 'Create a role to start assigning permissions.'}
                        </p>
                      </td>
                    </tr>
                  ) : (
                    roles.map((role) => (
                      <tr key={role.roleId} className="border-b border-[var(--color-border-light)]">
                        <td className="px-6 py-4">
                          <p className="font-medium text-[var(--color-text-primary)]">{role.name}</p>
                        </td>
                        <td className="px-6 py-4">
                          <p className="text-sm text-[var(--color-text-secondary)]">
                            {role.description || 'No description provided.'}
                          </p>
                        </td>
                        <td className="px-6 py-4">
                          <Badge variant="team" className="text-xs">
                            {role.permissionCount} permission{role.permissionCount === 1 ? '' : 's'}
                          </Badge>
                        </td>
                        <td className="px-6 py-4">
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

            {totalPages > 1 && (
              <div className="flex items-center justify-between px-6 py-4 border-t border-[var(--color-border-light)]">
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Showing {((pageNumber - 1) * pageSize) + 1} to {Math.min(pageNumber * pageSize, totalCount)} of {totalCount} roles
                </p>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={pageNumber <= 1}
                    onClick={() => setPageNumber((value) => value - 1)}
                  >
                    Previous
                  </Button>
                  <span className="text-sm text-[var(--color-text-primary)] px-2">
                    Page {pageNumber} of {totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={pageNumber >= totalPages}
                    onClick={() => setPageNumber((value) => value + 1)}
                  >
                    Next
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
