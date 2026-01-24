import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, Key, RefreshCw, Search } from 'lucide-react';
import { permissionService } from '@/services/permissionService';
import type { PermissionDefinition } from '@/types';

const categoryLabels: Record<string, string> = {
  Invoice: 'Billing',
  Payment: 'Payments',
  Ledger: 'Ledger',
  Settings: 'Settings',
  Users: 'Users',
  Roles: 'Roles',
  Platform: 'Platform',
};

const categoryBadgeStyles: Record<string, string> = {
  Billing: 'bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]',
  Payments: 'bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]',
  Ledger: 'bg-[var(--color-info-light)] text-[var(--color-info)]',
  Settings: 'bg-[var(--color-warning-light)] text-[var(--color-warning)]',
  Users: 'bg-[var(--color-success-light)] text-[var(--color-success)]',
  Roles: 'bg-[var(--color-pending-light)] text-[var(--color-pending)]',
  Platform: 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]',
};

export function AccessPermissionsPage() {
  const [permissions, setPermissions] = useState<PermissionDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');

  const loadPermissions = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await permissionService.list();
      setPermissions(response);
    } catch (err: unknown) {
      console.error('Failed to load permissions:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load permissions. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadPermissions();
  }, [loadPermissions]);

  const normalizedPermissions = useMemo(() => {
    return permissions.map((permission) => {
      const category = categoryLabels[permission.category] ?? permission.category;
      return {
        ...permission,
        displayCategory: category,
      };
    });
  }, [permissions]);

  const filteredPermissions = useMemo(() => {
    return normalizedPermissions.filter((permission) => {
      const matchesSearch = searchQuery
        ? `${permission.key} ${permission.description ?? ''}`
          .toLowerCase()
          .includes(searchQuery.toLowerCase())
        : true;
      const matchesCategory = categoryFilter ? permission.displayCategory === categoryFilter : true;
      return matchesSearch && matchesCategory;
    });
  }, [normalizedPermissions, searchQuery, categoryFilter]);

  const categories = useMemo(() => {
    return Array.from(new Set(normalizedPermissions.map((permission) => permission.displayCategory))).sort();
  }, [normalizedPermissions]);

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Permissions</h1>
            <p className="text-[var(--color-text-secondary)]">
              Review the global permission catalog available to tenant roles.
            </p>
          </div>
          <Button variant="outline" onClick={loadPermissions}>
            <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>

        <Card className="mb-6">
          <CardContent className="p-4">
            <div className="flex flex-wrap gap-4 items-end">
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
                    placeholder="Search permissions..."
                    className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                  />
                </div>
              </div>
              <div className="w-52">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Category
                </label>
                <select
                  value={categoryFilter}
                  onChange={(event) => setCategoryFilter(event.target.value)}
                  className="w-full px-3 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                >
                  <option value="">All Categories</option>
                  {categories.map((category) => (
                    <option key={category} value={category}>
                      {category}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </CardContent>
        </Card>

        {error && (
          <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5" />
              <span>{error}</span>
              <Button variant="outline" size="sm" onClick={loadPermissions} className="ml-auto">
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
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Permission</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Description</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Category</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={3} className="px-6 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading permissions...</p>
                      </td>
                    </tr>
                  ) : filteredPermissions.length === 0 ? (
                    <tr>
                      <td colSpan={3} className="px-6 py-12 text-center">
                        <Key className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No permissions found</p>
                        <p className="text-sm text-[var(--color-text-secondary)]">
                          {searchQuery || categoryFilter ? 'Try adjusting your filters.' : 'No permissions available yet.'}
                        </p>
                      </td>
                    </tr>
                  ) : (
                    filteredPermissions.map((permission) => {
                      const badgeStyle = categoryBadgeStyles[permission.displayCategory] ?? 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]';

                      return (
                        <tr key={permission.key} className="border-b border-[var(--color-border-light)]">
                          <td className="px-6 py-4">
                            <span className="font-mono text-sm text-[var(--color-text-primary)]">
                              {permission.key}
                            </span>
                          </td>
                          <td className="px-6 py-4">
                            <p className="text-sm text-[var(--color-text-secondary)]">
                              {permission.description || 'No description provided.'}
                            </p>
                          </td>
                          <td className="px-6 py-4">
                            <Badge className={`${badgeStyle} font-medium`}>
                              {permission.displayCategory}
                            </Badge>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
