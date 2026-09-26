import { useCallback, useEffect, useRef, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertCircle, RefreshCw, Search, Shield } from 'lucide-react';
import { roleService } from '@/services/roleService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { AccessRoleSummary, PagedResult } from '@/types';
import { DataTablePagination } from '@/components/ui/data-table';

export function AccessRolesPage() {
  const [roles, setRoles] = useState<AccessRoleSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
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
      setInitialLoad(false);
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

  if (initialLoad) {
    return <PageLoadingScreen message="Loading roles" />;
  }

  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Roles</h1>
          <p className="text-muted-foreground">
            Define reusable permission sets and assign them to teams.
          </p>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-destructive bg-destructive/10">
          <CardContent className="p-4 flex items-center gap-3 text-destructive">
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
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
              <Input
                type="text"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Search for roles"
                className="pl-10"
              />
            </div>

            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={loadRoles}
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
                  <TableHead className="px-4 text-xs text-muted-foreground">Role</TableHead>
                  <TableHead className="px-4 text-xs text-muted-foreground">Description</TableHead>
                  <TableHead className="px-4 text-xs text-muted-foreground">Permissions</TableHead>
                  <TableHead className="px-4 text-xs text-muted-foreground">Assigned users</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={4} className="px-4 py-12 text-center">
                      <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-muted-foreground" />
                      <p className="text-sm text-muted-foreground">Loading roles...</p>
                    </TableCell>
                  </TableRow>
                ) : roles.length === 0 ? (
                  <TableRow className="hover:bg-transparent">
                    <TableCell colSpan={4} className="px-4 py-12 text-center">
                      <div className="mb-3 flex justify-center text-muted-foreground">
                        <Shield className="w-12 h-12" />
                      </div>
                      <p className="text-foreground font-medium mb-1">No roles found</p>
                      <p className="text-sm text-muted-foreground">
                        {searchQuery ? 'Try adjusting your search.' : 'Create a role to start assigning permissions.'}
                      </p>
                    </TableCell>
                  </TableRow>
                ) : (
                  roles.map((role) => (
                    <TableRow key={role.roleId}>
                      <TableCell className="px-4 py-3">
                        <p className="font-medium text-foreground">{role.name}</p>
                      </TableCell>
                      <TableCell className="px-4 py-3 whitespace-normal">
                        <p className="text-sm text-muted-foreground">
                          {role.description || 'No description provided.'}
                        </p>
                      </TableCell>
                      <TableCell className="px-4 py-3">
                        <Badge variant="default" className="text-xs tabular-nums">
                          {role.permissionCount} permission{role.permissionCount === 1 ? '' : 's'}
                        </Badge>
                      </TableCell>
                      <TableCell className="px-4 py-3">
                        <Badge variant="outline" className="text-xs tabular-nums">
                          {role.userCount} user{role.userCount === 1 ? '' : 's'}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  ))
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
