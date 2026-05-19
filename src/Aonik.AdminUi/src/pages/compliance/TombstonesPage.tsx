import { useCallback, useEffect, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, Trash2 } from 'lucide-react';
import { userService } from '@/services/userService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { PagedResult, UserTombstoneSummary } from '@/types';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  type ColumnDef,
} from '@/components/ui/data-table';

/**
 * Spec 026 Part 2 — Compliance Tombstones page. Lists every user
 * deletion in this tenant: when, by whom, reason, masked email, count
 * of audit rows that were redacted as part of the right-to-be-forgotten
 * cleanup.
 */
export function TombstonesPage() {
  const [items, setItems] = useState<UserTombstoneSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<UserTombstoneSummary> = await userService.listTombstones({
        pageNumber,
        pageSize,
        search: searchQuery || undefined,
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load deletions. Please try again.');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, [pageNumber, pageSize, searchQuery]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery]);

  const formatDate = (value: string) =>
    new Date(value).toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });

  const columns: ColumnDef<UserTombstoneSummary>[] = [
    {
      id: 'deletedUtc',
      header: 'Deleted',
      accessorFn: (row) => new Date(row.deletedUtc),
      sortable: true,
      cell: (t) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {formatDate(t.deletedUtc)}
        </span>
      ),
    },
    {
      id: 'maskedEmail',
      header: 'User (masked)',
      accessorKey: 'maskedEmail',
      sortable: true,
      cell: (t) => (
        <div>
          <p className="font-medium text-[var(--color-text-primary)]">
            {t.maskedEmail ?? '(deleted)'}
          </p>
          <p className="text-xs text-[var(--color-text-tertiary)]">
            user-id: {t.originalUserId.slice(0, 8)}
          </p>
        </div>
      ),
    },
    {
      id: 'deletedBy',
      header: 'Operator',
      accessorFn: (row) => row.deletedByEmail ?? '',
      sortable: true,
      cell: (t) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {t.deletedByEmail ?? '(system)'}
        </span>
      ),
    },
    {
      id: 'reason',
      header: 'Reason',
      accessorKey: 'reason',
      sortable: false,
      cell: (t) => (
        <span className="text-sm text-[var(--color-text-primary)] line-clamp-2">
          {t.reason}
        </span>
      ),
    },
    {
      id: 'auditRowsRedacted',
      header: 'Audit redactions',
      accessorKey: 'auditRowsRedacted',
      sortable: true,
      cell: (t) => (
        <Badge variant="outline" className="text-xs">
          {t.auditRowsRedacted} row{t.auditRowsRedacted === 1 ? '' : 's'}
        </Badge>
      ),
    },
  ];

  if (initialLoad) {
    return <PageLoadingScreen message="Loading deleted users" />;
  }

  return (
    <div className="h-full overflow-auto p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Deleted users</h1>
          <p className="text-[var(--color-text-secondary)]">
            Audit log of hard deletions performed under GDPR / right-to-be-forgotten.
            The deleted user's PII has been redacted from audit logs; the tombstone
            below retains only the operator, timestamp, and reason for compliance review.
          </p>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={load} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <DataTableHeader
            searchValue={searchQuery}
            onSearchChange={setSearchQuery}
            searchPlaceholder="Search reasons or masked emails"
            className="px-0 border-b-0"
          />

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <DataTable
              data={items}
              columns={columns}
              getRowId={(t) => t.tombstoneId}
              loading={loading}
              loadingMessage="Loading deletions…"
              emptyIcon={<Trash2 className="w-12 h-12" />}
              emptyTitle="No deletions yet"
              emptyDescription={
                searchQuery
                  ? 'Try adjusting your filter.'
                  : 'When an operator permanently deletes a user, the tombstone will appear here.'
              }
            />
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={(s) => {
                setPageSize(s);
                setPageNumber(1);
              }}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
