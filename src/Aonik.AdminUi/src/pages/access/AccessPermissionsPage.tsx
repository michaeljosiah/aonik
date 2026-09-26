import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertCircle, Key, RefreshCw, Search } from 'lucide-react';
import { permissionService } from '@/services/permissionService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { PermissionDefinition } from '@/types';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

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
  Billing: 'bg-primary/10 text-primary',
  Payments: 'bg-agent/10 text-agent',
  Ledger: 'bg-info-subtle text-info',
  Settings: 'bg-warning-subtle text-warning',
  Users: 'bg-success-subtle text-success',
  Roles: 'bg-warning-subtle text-warning',
  Platform: 'bg-muted text-muted-foreground',
};

export function AccessPermissionsPage() {
  const [permissions, setPermissions] = useState<PermissionDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
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
      setInitialLoad(false);
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

  if (initialLoad) {
    return <PageLoadingScreen message="Loading permissions" />;
  }

  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Permissions</h1>
          <p className="text-muted-foreground">
            Review the global permission catalog available to tenant roles.
          </p>
        </div>
        <Button variant="outline" onClick={loadPermissions}>
          <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {error && (
        <Card className="mb-6 border-destructive bg-destructive/10">
          <CardContent className="p-4 flex items-center gap-3 text-destructive">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadPermissions} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-4 flex-1">
              <div className="relative w-96 max-w-full">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  type="text"
                  value={searchQuery}
                  onChange={(event) => setSearchQuery(event.target.value)}
                  placeholder="Search for permissions"
                  className="pl-10"
                />
              </div>

              <Select
                value={categoryFilter || undefined}
                onValueChange={(value) => setCategoryFilter(value === '__all__' ? '' : value)}
              >
                <SelectTrigger aria-label="Filter by category" className="w-56">
                  <SelectValue placeholder="Filter by category" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All categories</SelectItem>
                  {categories.map((category) => (
                    <SelectItem key={category} value={category}>
                      {category}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="mt-3 rounded-md border border-border overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50 hover:bg-muted/50">
                  <TableHead className="px-4 text-xs text-muted-foreground">Permission</TableHead>
                  <TableHead className="px-4 text-xs text-muted-foreground">Description</TableHead>
                  <TableHead className="px-4 text-xs text-muted-foreground">Category</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={3} className="px-4 py-12 text-center">
                      <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-muted-foreground" />
                      <p className="text-sm text-muted-foreground">Loading permissions...</p>
                    </TableCell>
                  </TableRow>
                ) : filteredPermissions.length === 0 ? (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={3} className="px-4 py-12 text-center">
                      <div className="mb-3 flex justify-center text-muted-foreground">
                        <Key className="w-12 h-12" />
                      </div>
                      <p className="text-foreground font-medium mb-1">No permissions found</p>
                      <p className="text-sm text-muted-foreground">
                        {searchQuery || categoryFilter ? 'Try adjusting your filters.' : 'No permissions available yet.'}
                      </p>
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredPermissions.map((permission) => {
                    const badgeStyle = categoryBadgeStyles[permission.displayCategory] ?? 'bg-muted text-muted-foreground';

                    return (
                      <TableRow key={permission.key}>
                        <TableCell className="px-4 py-3">
                          <span className="font-mono text-sm text-foreground">
                            {permission.key}
                          </span>
                        </TableCell>
                        <TableCell className="px-4 py-3 whitespace-normal">
                          <p className="text-sm text-muted-foreground">
                            {permission.description || 'No description provided.'}
                          </p>
                        </TableCell>
                        <TableCell className="px-4 py-3">
                          <Badge className={`${badgeStyle} font-medium`}>
                            {permission.displayCategory}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
