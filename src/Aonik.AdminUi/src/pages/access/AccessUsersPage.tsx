import { useCallback, useEffect, useRef, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, RefreshCw, Search, UserPlus, Users } from 'lucide-react';
import { userService } from '@/services/userService';
import type { AccessUserSummary, PagedResult } from '@/types';

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Invited: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Deactivated: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
  Suspended: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

export function AccessUsersPage() {
  const [users, setUsers] = useState<AccessUserSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);
  const requestIdRef = useRef(0);

  const loadUsers = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<AccessUserSummary> = await userService.list({
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        search: searchQuery || undefined,
      });
      if (requestIdRef.current != requestId)
      {
        return;
      }
      setUsers(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load users:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      if (requestIdRef.current != requestId)
      {
        return;
      }
      setError(message || 'Failed to load users. Please try again.');
    } finally {
      if (requestIdRef.current != requestId)
      {
        return;
      }
      setLoading(false);
    }
  }, [pageNumber, pageSize, searchQuery, statusFilter]);

  useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  const totalPages = Math.ceil(totalCount / pageSize);

  const handleSearch = (event: React.FormEvent) => {
    event.preventDefault();
    setPageNumber(1);
  };

  const formatDate = (dateString?: string | null) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Users</h1>
            <p className="text-[var(--color-text-secondary)]">
              Manage tenant users, invitations, and access status.
            </p>
          </div>
          <Button disabled title="Invite flow coming soon">
            <UserPlus className="w-4 h-4 mr-2" />
            Invite User
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
                    placeholder="Search by name or email..."
                    className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                  />
                </div>
              </div>
              <div className="w-40">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Status
                </label>
                <select
                  value={statusFilter}
                  onChange={(event) => { setStatusFilter(event.target.value); setPageNumber(1); }}
                  className="w-full px-3 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                >
                  <option value="">All Statuses</option>
                  <option value="Active">Active</option>
                  <option value="Invited">Invited</option>
                  <option value="Pending">Pending</option>
                  <option value="Deactivated">Deactivated</option>
                  <option value="Suspended">Suspended</option>
                </select>
              </div>
              <div className="flex gap-2">
                <Button type="submit" variant="outline">
                  <Search className="w-4 h-4 mr-2" />
                  Search
                </Button>
                <Button type="button" variant="ghost" onClick={loadUsers}>
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
              <Button variant="outline" size="sm" onClick={loadUsers} className="ml-auto">
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
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">User</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Status</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Roles</th>
                    <th className="text-left px-6 py-4 text-sm font-semibold text-[var(--color-text-secondary)]">Last Login</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={4} className="px-6 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading users...</p>
                      </td>
                    </tr>
                  ) : users.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-6 py-12 text-center">
                        <Users className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No users found</p>
                        <p className="text-sm text-[var(--color-text-secondary)]">
                          {searchQuery || statusFilter ? 'Try adjusting your filters.' : 'Invite your first teammate to get started.'}
                        </p>
                      </td>
                    </tr>
                  ) : (
                    users.map((user) => {
                      const style = statusStyles[user.status] ?? {
                        text: 'text-[var(--color-text-secondary)]',
                        bg: 'bg-[var(--color-surface-inset)]',
                      };

                      return (
                        <tr key={user.userId} className="border-b border-[var(--color-border-light)]">
                          <td className="px-6 py-4">
                            <div>
                              <p className="font-medium text-[var(--color-text-primary)]">
                                {user.displayName || user.email}
                              </p>
                              <p className="text-xs text-[var(--color-text-tertiary)]">{user.email}</p>
                            </div>
                          </td>
                          <td className="px-6 py-4">
                            <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}>
                              {user.status}
                            </span>
                          </td>
                          <td className="px-6 py-4">
                            <Badge variant="team" className="text-xs">
                              {user.roleCount} role{user.roleCount === 1 ? '' : 's'}
                            </Badge>
                          </td>
                          <td className="px-6 py-4">
                            <span className="text-sm text-[var(--color-text-secondary)]">
                              {formatDate(user.lastLoginAt)}
                            </span>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-between px-6 py-4 border-t border-[var(--color-border-light)]">
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Showing {((pageNumber - 1) * pageSize) + 1} to {Math.min(pageNumber * pageSize, totalCount)} of {totalCount} users
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
