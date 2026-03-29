import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  AlertCircle,
  ArrowLeft,
  CreditCard,
  FileUp,
  Landmark,
  Link2Off,
  Loader2,
  Paperclip,
  Plus,
  RefreshCw,
  RotateCcw,
  Trash2,
} from 'lucide-react';

import { accountService } from '@/services/accountService';
import type {
  AccountConnectionResponse,
  AccountTransactionResponse,
  AccountTransactionAttachmentResponse,
  PagedResult,
} from '@/types';
import {
  DataTable,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';
import { CreateTransactionDialog } from './CreateTransactionDialog';

const statusStyles: Record<string, { text: string; bg: string }> = {
  Connected: {
    text: 'text-[var(--color-success)]',
    bg: 'bg-[var(--color-success-light)]',
  },
  ActionRequired: {
    text: 'text-[var(--color-warning)]',
    bg: 'bg-[var(--color-warning-light)]',
  },
  Disconnected: {
    text: 'text-[var(--color-text-tertiary)]',
    bg: 'bg-[var(--color-surface-inset)]',
  },
};

const reconciliationStyles: Record<string, { text: string; bg: string }> = {
  Matched: {
    text: 'text-[var(--color-success)]',
    bg: 'bg-[var(--color-success-light)]',
  },
  Unmatched: {
    text: 'text-[var(--color-warning)]',
    bg: 'bg-[var(--color-warning-light)]',
  },
  Excluded: {
    text: 'text-[var(--color-text-tertiary)]',
    bg: 'bg-[var(--color-surface-inset)]',
  },
};

function formatDate(dateString?: string | null): string {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatDateTime(dateString?: string | null): string {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatCurrency(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export function AccountConnectionDetailPage() {
  const { connectionId } = useParams<{ connectionId: string }>();
  const navigate = useNavigate();

  const [connection, setConnection] = useState<AccountConnectionResponse | null>(null);
  const [transactions, setTransactions] = useState<AccountTransactionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [txLoading, setTxLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalTxCount, setTotalTxCount] = useState(0);
  const [showCreateTransaction, setShowCreateTransaction] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploadingTxId, setUploadingTxId] = useState<string | null>(null);
  const [attachmentsDialogTxId, setAttachmentsDialogTxId] = useState<string | null>(null);
  const [attachments, setAttachments] = useState<AccountTransactionAttachmentResponse[]>([]);
  const [attachmentsLoading, setAttachmentsLoading] = useState(false);

  const loadConnection = useCallback(async () => {
    if (!connectionId) return;
    setLoading(true);
    setError(null);
    try {
      const all = await accountService.listConnections(true);
      const found = all.find((c) => c.connectionId === connectionId);
      if (!found) {
        setError('Connection not found.');
        setLoading(false);
        return;
      }
      setConnection(found);
      setLoading(false);
    } catch (err: unknown) {
      console.error('Failed to load connection:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load connection details.');
      setLoading(false);
    }
  }, [connectionId]);

  const loadTransactions = useCallback(async () => {
    if (!connectionId) return;
    setTxLoading(true);
    try {
      const result: PagedResult<AccountTransactionResponse> =
        await accountService.listTransactions({
          connectionId,
          pageNumber,
          pageSize,
        });
      setTransactions(result.items);
      setTotalTxCount(result.totalCount);
      setTxLoading(false);
    } catch (err: unknown) {
      console.error('Failed to load transactions:', err);
      setTxLoading(false);
    }
  }, [connectionId, pageNumber, pageSize]);

  useEffect(() => {
    loadConnection();
  }, [loadConnection]);

  useEffect(() => {
    loadTransactions();
  }, [loadTransactions]);

  const handleRefresh = useCallback(async () => {
    if (!connectionId) return;
    setActionLoading(true);
    try {
      await accountService.refreshConnection(connectionId);
      toast.success('Connection refreshed successfully.');
      await loadConnection();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to refresh connection';
      toast.error(message);
    } finally {
      setActionLoading(false);
    }
  }, [connectionId, loadConnection]);

  const handleSync = useCallback(async () => {
    if (!connectionId) return;
    setActionLoading(true);
    try {
      const result = await accountService.syncTransactions(connectionId);
      toast.success(
        `Sync complete: ${result.transactionsAdded} added, ${result.transactionsUpdated} updated.`
      );
      await loadConnection();
      await loadTransactions();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to sync transactions';
      toast.error(message);
    } finally {
      setActionLoading(false);
    }
  }, [connectionId, loadConnection, loadTransactions]);

  const handleDisconnect = useCallback(async () => {
    if (!connectionId || !connection) return;
    if (!window.confirm(`Are you sure you want to disconnect ${connection.institutionName}?`)) return;
    setActionLoading(true);
    try {
      await accountService.disconnectConnection(connectionId);
      toast.success(`${connection.institutionName} disconnected.`);
      navigate('/accounts');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to disconnect';
      toast.error(message);
    } finally {
      setActionLoading(false);
    }
  }, [connectionId, connection, navigate]);

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };

  const handleUploadFile = (transactionId: string) => {
    setUploadingTxId(transactionId);
    fileInputRef.current?.click();
  };

  const handleFileSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !uploadingTxId) {
      setUploadingTxId(null);
      return;
    }
    try {
      await accountService.uploadAttachment(uploadingTxId, file);
      toast.success(`File "${file.name}" uploaded successfully.`);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to upload file';
      const userMessage =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(userMessage || message);
    } finally {
      setUploadingTxId(null);
      // Reset file input so the same file can be re-selected
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleViewAttachments = async (transactionId: string) => {
    setAttachmentsDialogTxId(transactionId);
    setAttachmentsLoading(true);
    try {
      const result = await accountService.listAttachments(transactionId);
      setAttachments(result);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to load attachments';
      toast.error(message);
      setAttachments([]);
    } finally {
      setAttachmentsLoading(false);
    }
  };

  const handleDeleteAttachment = async (attachmentId: string) => {
    if (!window.confirm('Are you sure you want to delete this attachment?')) return;
    try {
      await accountService.deleteAttachment(attachmentId);
      toast.success('Attachment deleted.');
      setAttachments((prev) => prev.filter((a) => a.attachmentId !== attachmentId));
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to delete attachment';
      toast.error(message);
    }
  };

  const getTxRowActions = (tx: AccountTransactionResponse): DataTableAction[] => [
    {
      label: 'Upload File',
      icon: <FileUp className="w-4 h-4" />,
      onClick: () => handleUploadFile(tx.transactionId),
    },
    {
      label: 'View Attachments',
      icon: <Paperclip className="w-4 h-4" />,
      onClick: () => handleViewAttachments(tx.transactionId),
    },
  ];

  const txColumns: ColumnDef<AccountTransactionResponse>[] = [
    {
      id: 'occurredAt',
      header: 'Date',
      accessorFn: (row) => (row.occurredAt ? new Date(row.occurredAt) : null),
      sortable: true,
      cell: (tx) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{formatDate(tx.occurredAt)}</span>
      ),
    },
    {
      id: 'amount',
      header: 'Amount',
      accessorKey: 'amount',
      sortable: true,
      cell: (tx) => (
        <span
          className={`text-sm font-medium ${tx.amount < 0 ? 'text-[var(--color-error)]' : 'text-[var(--color-success)]'}`}
        >
          {formatCurrency(tx.amount, tx.currency)}
        </span>
      ),
    },
    {
      id: 'currency',
      header: 'Currency',
      accessorKey: 'currency',
      sortable: true,
      cell: (tx) => (
        <Badge variant="outline" className="text-xs">
          {tx.currency}
        </Badge>
      ),
    },
    {
      id: 'counterparty',
      header: 'Counterparty',
      accessorKey: 'counterparty',
      sortable: true,
      cell: (tx) => (
        <span className="text-sm text-[var(--color-text-primary)]">{tx.counterparty || '—'}</span>
      ),
    },
    {
      id: 'description',
      header: 'Description',
      accessorKey: 'description',
      sortable: false,
      cell: (tx) => (
        <span className="text-sm text-[var(--color-text-secondary)] truncate max-w-[200px] block">
          {tx.description || '—'}
        </span>
      ),
    },
    {
      id: 'reconciliationStatus',
      header: 'Reconciliation',
      accessorKey: 'reconciliationStatus',
      sortable: true,
      cell: (tx) => {
        const style = reconciliationStyles[tx.reconciliationStatus] ?? {
          text: 'text-[var(--color-text-secondary)]',
          bg: 'bg-[var(--color-surface-inset)]',
        };
        return (
          <span
            className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}
          >
            {tx.reconciliationStatus}
          </span>
        );
      },
    },
  ];

  const breadcrumbItems = [
    { label: 'Accounts', icon: <Landmark className="w-3.5 h-3.5" />, href: '/accounts' },
    { label: connection?.institutionName || 'Connection Details' },
  ];

  if (loading) {
    return (
      <div className="h-full overflow-auto p-6">
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-6 h-6 animate-spin text-[var(--color-text-tertiary)]" />
          <span className="ml-2 text-[var(--color-text-secondary)]">Loading connection details...</span>
        </div>
      </div>
    );
  }

  if (error || !connection) {
    return (
      <div className="h-full overflow-auto p-6">
        <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error || 'Connection not found.'}</span>
            <Button variant="outline" size="sm" onClick={() => navigate('/accounts')} className="ml-auto">
              Back to Accounts
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const statusStyle = statusStyles[connection.status] ?? {
    text: 'text-[var(--color-text-secondary)]',
    bg: 'bg-[var(--color-surface-inset)]',
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="sm" onClick={() => navigate('/accounts')}>
            <ArrowLeft className="w-4 h-4 mr-1" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
                {connection.institutionName}
              </h1>
              <span
                className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${statusStyle.bg} ${statusStyle.text}`}
              >
                {connection.status}
              </span>
            </div>
            <p className="text-[var(--color-text-secondary)]">
              {connection.providerDisplayName} &middot; Connected {formatDate(connection.createdAt)}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => setShowCreateTransaction(true)}>
            <Plus className="w-4 h-4 mr-1" />
            Add Transaction
          </Button>
          <Button variant="outline" size="sm" onClick={handleRefresh} disabled={actionLoading}>
            {actionLoading ? <Loader2 className="w-4 h-4 mr-1 animate-spin" /> : <RefreshCw className="w-4 h-4 mr-1" />}
            Refresh
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={handleSync}
            disabled={actionLoading || connection.status === 'Disconnected'}
          >
            {actionLoading ? <Loader2 className="w-4 h-4 mr-1 animate-spin" /> : <RotateCcw className="w-4 h-4 mr-1" />}
            Sync Transactions
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={handleDisconnect}
            disabled={actionLoading || connection.status === 'Disconnected'}
            className="text-[var(--color-error)] border-[var(--color-error)] hover:bg-[var(--color-error-light)]"
          >
            <Link2Off className="w-4 h-4 mr-1" />
            Disconnect
          </Button>
        </div>
      </div>

      {/* Connection info */}
      {connection.lastError && (
        <Card className="mb-6 border-[var(--color-warning)] bg-[var(--color-warning-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-warning)]">
            <AlertCircle className="w-5 h-5" />
            <span className="text-sm">{connection.lastError}</span>
          </CardContent>
        </Card>
      )}

      {/* Linked Accounts */}
      <div className="mb-6">
        <h2 className="text-lg font-semibold text-[var(--color-text-primary)] mb-3">
          Linked Accounts ({connection.linkedAccounts.length})
        </h2>
        {connection.linkedAccounts.length === 0 ? (
          <Card className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <CardContent className="p-6 text-center text-[var(--color-text-tertiary)]">
              No linked accounts found for this connection.
            </CardContent>
          </Card>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {connection.linkedAccounts.map((account) => (
              <Card
                key={account.linkedAccountId}
                className="rounded-none border-[var(--color-border-light)] bg-[var(--color-surface)]"
              >
                <CardContent className="p-4">
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex items-center gap-2">
                      <CreditCard className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      <p className="font-medium text-[var(--color-text-primary)]">{account.name}</p>
                    </div>
                    <Badge variant="outline" className="text-xs">
                      {account.status}
                    </Badge>
                  </div>
                  <div className="space-y-1 text-sm text-[var(--color-text-secondary)]">
                    <p>
                      <span className="text-[var(--color-text-tertiary)]">Type:</span>{' '}
                      {account.accountType}
                      {account.accountSubtype ? ` / ${account.accountSubtype}` : ''}
                    </p>
                    <p>
                      <span className="text-[var(--color-text-tertiary)]">Currency:</span> {account.currency}
                    </p>
                    {account.last4 && (
                      <p>
                        <span className="text-[var(--color-text-tertiary)]">Last 4:</span> ****{account.last4}
                      </p>
                    )}
                    <p>
                      <span className="text-[var(--color-text-tertiary)]">Last synced:</span>{' '}
                      {formatDateTime(account.lastSyncedAt)}
                    </p>
                  </div>
                  {account.lastError && (
                    <p className="mt-2 text-xs text-[var(--color-error)]">{account.lastError}</p>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Transactions */}
      <div>
        <h2 className="text-lg font-semibold text-[var(--color-text-primary)] mb-3">Transactions</h2>
        <Card>
          <CardContent className="p-4">
            <div className="rounded-md border border-[var(--color-border-light)] overflow-hidden">
              <DataTable
                data={transactions}
                columns={txColumns}
                getRowId={(tx) => tx.transactionId}
                loading={txLoading}
                loadingMessage="Loading transactions..."
                emptyIcon={<Landmark className="w-12 h-12" />}
                emptyTitle="No transactions yet"
                emptyDescription="Sync transactions to see them here."
                rowActions={(tx) => <DataTableRowActions actions={getTxRowActions(tx)} />}
                rowActionsPosition="start"
              />
            </div>

            <div className="pt-4">
              <DataTablePagination
                pageNumber={pageNumber}
                pageSize={pageSize}
                totalCount={totalTxCount}
                onPageChange={setPageNumber}
                onPageSizeChange={handlePageSizeChange}
                className="px-0 border-t-0"
              />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Hidden file input for uploads */}
      <input
        ref={fileInputRef}
        type="file"
        className="hidden"
        onChange={handleFileSelected}
      />

      {/* Create Transaction Dialog */}
      <CreateTransactionDialog
        open={showCreateTransaction}
        onOpenChange={setShowCreateTransaction}
        onSuccess={() => {
          loadTransactions();
          loadConnection();
        }}
      />

      {/* Attachments Dialog */}
      <Dialog
        open={!!attachmentsDialogTxId}
        onOpenChange={(open) => {
          if (!open) {
            setAttachmentsDialogTxId(null);
            setAttachments([]);
          }
        }}
      >
        <DialogContent className="sm:max-w-[500px] max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Transaction Attachments</DialogTitle>
          </DialogHeader>
          {attachmentsLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="w-5 h-5 animate-spin text-[var(--color-text-tertiary)]" />
              <span className="ml-2 text-sm text-[var(--color-text-secondary)]">Loading...</span>
            </div>
          ) : attachments.length === 0 ? (
            <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
              No attachments found for this transaction.
            </p>
          ) : (
            <div className="space-y-3">
              {attachments.map((att) => (
                <div
                  key={att.attachmentId}
                  className="flex items-center justify-between p-3 border border-[var(--color-border-light)] rounded-sm"
                >
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-[var(--color-text-primary)] truncate">
                      {att.fileName}
                    </p>
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      {att.contentType} &middot; {(att.fileSizeBytes / 1024).toFixed(1)} KB
                    </p>
                  </div>
                  <div className="flex items-center gap-2 ml-3">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleDeleteAttachment(att.attachmentId)}
                      className="text-[var(--color-error)] hover:bg-[var(--color-error-light)]"
                    >
                      <Trash2 className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
