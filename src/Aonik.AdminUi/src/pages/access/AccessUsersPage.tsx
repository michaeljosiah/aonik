import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { AlertCircle, Edit, Eye, User, UserMinus, Users, UsersRound } from 'lucide-react';
import { userService } from '@/services/userService';
import type { AccessUserSummary, PagedResult } from '@/types';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  DataTableGridView,
  type ColumnDef,
  type ViewMode,
  type DataTableAction,
  type FilterOption,
} from '@/components/ui/data-table';

const statusStyles: Record<string, { text: string; bg: string; iconColor: string }> = {
  Active: { 
    text: 'text-[var(--color-success)]', 
    bg: 'bg-[var(--color-success-light)]',
    iconColor: 'text-[var(--color-brand-primary)]'
  },
  Invited: { 
    text: 'text-[var(--color-warning)]', 
    bg: 'bg-[var(--color-warning-light)]',
    iconColor: 'text-[var(--color-warning)]'
  },
  Pending: { 
    text: 'text-[var(--color-warning)]', 
    bg: 'bg-[var(--color-warning-light)]',
    iconColor: 'text-[var(--color-warning)]'
  },
  Deactivated: { 
    text: 'text-[var(--color-text-tertiary)]', 
    bg: 'bg-[var(--color-surface-inset)]',
    iconColor: 'text-[var(--color-text-tertiary)]'
  },
  Suspended: { 
    text: 'text-[var(--color-error)]', 
    bg: 'bg-[var(--color-error-light)]',
    iconColor: 'text-[var(--color-error)]'
  },
};

const statusFilterOptions: FilterOption[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Invited', label: 'Invited' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Deactivated', label: 'Deactivated' },
  { value: 'Suspended', label: 'Suspended' },
];

export function AccessUsersPage() {
  const navigate = useNavigate();
  const [users, setUsers] = useState<AccessUserSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);
  const [viewMode, setViewMode] = useState<ViewMode>('list');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const requestIdRef = useRef(0);
  const [imageLoadStates, setImageLoadStates] = useState<Record<string, 'loading' | 'loaded' | 'error'>>({});
  const [imageErrors, setImageErrors] = useState<Set<string>>(new Set());

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
      if (requestIdRef.current !== requestId) {
        return;
      }
      setUsers(result.items);
      setTotalCount(result.totalCount);
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) {
        return;
      }
      console.error('Failed to load users:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load users. Please try again.');
      setLoading(false);
    }
  }, [pageNumber, pageSize, searchQuery, statusFilter]);

  useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  // Reset to page 1 when search or filter changes
  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter]);

  // Reset image states when users change
  useEffect(() => {
    setImageLoadStates({});
    setImageErrors(new Set());
  }, [users]);

  // Handle image load
  const handleImageLoad = useCallback((userId: string) => {
    setImageLoadStates(prev => ({ ...prev, [userId]: 'loaded' }));
  }, []);

  // Handle image error
  const handleImageError = useCallback((userId: string) => {
    setImageLoadStates(prev => ({ ...prev, [userId]: 'error' }));
    setImageErrors(prev => new Set([...prev, userId]));
  }, []);

  const formatDate = (dateString?: string | null) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  // Helper to get photo URL with fallback
  const getPhotoUrl = (user: AccessUserSummary, size: 'tiny' | 'small' = 'tiny') => {
    const photoUrl = size === 'tiny' 
      ? (user.photoUrlTiny || user.photoUrlSmall || user.photoUrl)
      : (user.photoUrlSmall || user.photoUrlTiny || user.photoUrl);
    
    if (!photoUrl) return null;
    if (photoUrl.startsWith('http')) return photoUrl;
    
    const apiBaseUrl = import.meta.env.VITE_API_URL || 'https://localhost:5001';
    return `${apiBaseUrl}${photoUrl}`;
  };

  // Get user initials for fallback avatar
  const getUserInitials = (user: AccessUserSummary) => {
    const name = user.displayName || user.email;
    return name
      .split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  };

  const getRowActions = (user: AccessUserSummary): DataTableAction[] => [
    {
      label: 'View Details',
      icon: <Eye className="w-4 h-4" />,
      onClick: () => navigate(`/access/users/${user.userId}`),
    },
    {
      label: 'Edit User',
      icon: <Edit className="w-4 h-4" />,
      onClick: () => navigate(`/access/users/${user.userId}`),
    },
    {
      label: 'Deactivate',
      icon: <UserMinus className="w-4 h-4" />,
      onClick: () => console.log('Deactivate user:', user.userId),
      variant: 'danger',
      disabled: user.status === 'Deactivated',
    },
  ];

  // Render user avatar based on photo or initials
  const renderUserIcon = (user: AccessUserSummary) => {
    const style = statusStyles[user.status] ?? { iconColor: 'text-[var(--color-text-tertiary)]' };
    const photoUrl = getPhotoUrl(user, 'tiny');
    const initials = getUserInitials(user);
    const hasError = imageErrors.has(user.userId);
    const isLoading = imageLoadStates[user.userId] === 'loading';
    
    return (
      <Avatar className="w-6 h-6">
        {photoUrl && !hasError ? (
          <>
            <AvatarImage 
              src={photoUrl} 
              alt={user.displayName || user.email}
              onLoad={() => handleImageLoad(user.userId)}
              onError={() => handleImageError(user.userId)}
              className={isLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
            />
            {isLoading && (
              <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                <div className="w-3 h-3 border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin" />
              </div>
            )}
          </>
        ) : null}
        <AvatarFallback className={`text-xs ${style.iconColor} bg-[var(--color-surface-inset)]`}>
          {initials}
        </AvatarFallback>
      </Avatar>
    );
  };

  const columns: ColumnDef<AccessUserSummary>[] = [
    {
      id: 'user',
      header: 'User',
      accessorFn: (row) => row.displayName || row.email,
      sortable: true,
      cell: (user) => (
        <div>
          <p className="font-medium text-[var(--color-text-primary)]">
            {user.displayName || user.email}
          </p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{user.email}</p>
        </div>
      ),
    },
    {
      id: 'party',
      header: 'Party',
      accessorFn: (row) => row.partyDisplayName || row.partyType || '',
      sortable: true,
      cell: (user) => (
        <div className="space-y-1">
          <p className="text-sm text-[var(--color-text-primary)]">
            {user.partyDisplayName ?? 'Not linked'}
          </p>
          {user.partyType && (
            <Badge variant="outline" className="text-xs">
              {user.partyType}
            </Badge>
          )}
        </div>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (user) => {
        const style = statusStyles[user.status] ?? {
          text: 'text-[var(--color-text-secondary)]',
          bg: 'bg-[var(--color-surface-inset)]',
        };
        return (
          <span
            className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}
          >
            {user.status}
          </span>
        );
      },
    },
    {
      id: 'roles',
      header: 'Roles',
      accessorKey: 'roleCount',
      sortable: true,
      cell: (user) => (
        <Badge variant="team" className="text-xs">
          {user.roleCount} role{user.roleCount === 1 ? '' : 's'}
        </Badge>
      ),
    },
    {
      id: 'lastLogin',
      header: 'Last Login',
      accessorFn: (row) => row.lastLoginAt ? new Date(row.lastLoginAt) : null,
      sortable: true,
      cell: (user) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {formatDate(user.lastLoginAt)}
        </span>
      ),
    },
  ];

  const renderUserCard = (user: AccessUserSummary) => {
    const style = statusStyles[user.status] ?? {
      text: 'text-[var(--color-text-secondary)]',
      bg: 'bg-[var(--color-surface-inset)]',
      iconColor: 'text-[var(--color-text-tertiary)]',
    };
    const photoUrl = getPhotoUrl(user, 'small');
    const initials = getUserInitials(user);
    const hasError = imageErrors.has(user.userId);
    const isLoading = imageLoadStates[user.userId] === 'loading';

    return (
      <div className="space-y-3">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            <Avatar className="w-8 h-8 relative">
              {photoUrl && !hasError ? (
                <>
                  <AvatarImage 
                    src={photoUrl} 
                    alt={user.displayName || user.email}
                    onLoad={() => handleImageLoad(user.userId)}
                    onError={() => handleImageError(user.userId)}
                    className={isLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
                  />
                  {isLoading && (
                    <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                      <div className="w-4 h-4 border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin" />
                    </div>
                  )}
                </>
              ) : null}
              <AvatarFallback className={`text-sm ${style.iconColor} bg-[var(--color-surface-inset)]`}>
                {initials}
              </AvatarFallback>
            </Avatar>
            <div>
              <p className="font-medium text-[var(--color-text-primary)]">
                {user.displayName || user.email}
              </p>
              <p className="text-xs text-[var(--color-text-tertiary)]">{user.email}</p>
            </div>
          </div>
          <DataTableRowActions actions={getRowActions(user)} />
        </div>
        <div className="flex items-center gap-3">
          <span
            className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}
          >
            {user.status}
          </span>
          <Badge variant="team" className="text-xs">
            {user.roleCount} role{user.roleCount === 1 ? '' : 's'}
          </Badge>
        </div>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Party</p>
            <p className="text-sm text-[var(--color-text-primary)]">
              {user.partyDisplayName ?? 'Not linked'}
            </p>
          </div>
          {user.partyType && (
            <Badge variant="outline" className="text-xs">
              {user.partyType}
            </Badge>
          )}
        </div>
        <p className="text-xs text-[var(--color-text-secondary)]">
          Last login: {formatDate(user.lastLoginAt)}
        </p>
      </div>
    );
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };

  const breadcrumbItems = [
    { label: 'Users & Access', href: '/access', icon: <UsersRound className="w-3.5 h-3.5" /> },
    { label: 'Users', icon: <User className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      {/* Breadcrumb */}
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

        {/* Page Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Users</h1>
            <p className="text-[var(--color-text-secondary)]">
              Manage tenant users, invitations, and access status.
            </p>
          </div>
        </div>

        {/* Error State */}
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

        {/* Main Data Card */}
        <Card>
          <CardContent className="p-4">
            {/* Header with Search, Filter, and View Toggle */}
            <DataTableHeader
              searchValue={searchQuery}
              onSearchChange={setSearchQuery}
              searchPlaceholder="Search for users"
              filterValue={statusFilter}
              onFilterChange={setStatusFilter}
              filterOptions={statusFilterOptions}
              filterPlaceholder="Filter by status"
              viewMode={viewMode}
              onViewModeChange={setViewMode}
              showViewToggle={true}
              className="px-0 border-b-0"
            />

            {/* Table or Grid View */}
            <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
              {viewMode === 'list' ? (
                <DataTable
                  data={users}
                  columns={columns}
                  getRowId={(user) => user.userId}
                  selectedIds={selectedIds}
                  onSelectionChange={setSelectedIds}
                  showCheckboxes={true}
                  rowIcon={renderUserIcon}
                  loading={loading}
                  loadingMessage="Loading users..."
                  emptyIcon={<Users className="w-12 h-12" />}
                  emptyTitle="No users found"
                  emptyDescription={
                    searchQuery || statusFilter
                      ? 'Try adjusting your filters.'
                      : 'Invite your first teammate to get started.'
                  }
                  rowActions={(user) => <DataTableRowActions actions={getRowActions(user)} />}
                  rowActionsPosition="start"
                />
              ) : (
                <DataTableGridView
                  data={users}
                  getRowId={(user) => user.userId}
                  renderCard={renderUserCard}
                  selectedIds={selectedIds}
                  onSelectionChange={setSelectedIds}
                  showCheckboxes={true}
                  loading={loading}
                  loadingMessage="Loading users..."
                  emptyIcon={<Users className="w-12 h-12" />}
                  emptyTitle="No users found"
                  emptyDescription={
                    searchQuery || statusFilter
                      ? 'Try adjusting your filters.'
                      : 'Invite your first teammate to get started.'
                  }
                  columns={3}
                />
              )}
            </div>

            {/* Pagination */}
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
