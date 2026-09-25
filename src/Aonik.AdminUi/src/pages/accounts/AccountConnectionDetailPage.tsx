import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
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

type StatusBadgeVariant = 'success' | 'warning' | 'secondary';

const statusVariants: Record<string, StatusBadgeVariant> = {
  Connected: 'success',
  ActionRequired: 'warning',
  Disconnected: 'secondary',
};

const reconciliationVariants: Record<string, StatusBadgeVariant> = {
  Matched: 'success',
  Unmatched: 'warning',
  Excluded: 'secondary',
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
  const [confirmDisconnectOpen, setConfirmDisconnectOpen] = useState(false);
  const [pendingDeleteAttachmentId, setPendingDeleteAttachmentId] = useState<string | null>(null);

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
        <span className="text-sm text-muted-foreground">{formatDate(tx.occurredAt)}</span>
      ),
    },
    {
      id: 'amount',
      header: 'Amount',
      accessorKey: 'amount',
      sortable: true,
      numeric: true,
      cell: (tx) => (
        <span
          className={`text-sm font-medium ${tx.amount < 0 ? 'text-destructive' : 'text-success'}`}
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
        <span className="text-sm text-foreground">{tx.counterparty || '—'}</span>
      ),
    },
    {
      id: 'description',
      header: 'Description',
      accessorKey: 'description',
      sortable: false,
      cell: (tx) => (
        <span className="text-sm text-muted-foreground truncate max-w-[200px] block">
          {tx.description || '—'}
        </span>
      ),
    },
    {
      id: 'reconciliationStatus',
      header: 'Reconciliation',
      accessorKey: 'reconciliationStatus',
      sortable: true,
      cell: (tx) => (
        <Badge variant={reconciliationVariants[tx.reconciliationStatus] ?? 'secondary'}>
          {tx.reconciliationStatus}
        </Badge>
      ),
    },
  ];
  if (loading) {
    return <PageLoadingScreen message="Loading connection details" />;
  }

  if (error || !connection) {
    return (
      <div className="h-full overflow-auto p-6">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription className="flex w-full items-center gap-3">
            <span>{error || 'Connection not found.'}</span>
            <Button variant="outline" size="sm" onClick={() => navigate('/accounts')} className="ml-auto">
              Back to Accounts
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6">

      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="sm" onClick={() => navigate('/accounts')}>
            <ArrowLeft className="w-4 h-4 mr-1" />
            Back
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-foreground">
                {connection.institutionName}
              </h1>
              <Badge variant={statusVariants[connection.status] ?? 'secondary'}>
                {connection.status}
              </Badge>
            </div>
            <p className="text-muted-foreground">
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
            onClick={() => setConfirmDisconnectOpen(true)}
            disabled={actionLoading || connection.status === 'Disconnected'}
            className="text-destructive border-destructive hover:bg-destructive/10"
          >
            <Link2Off className="w-4 h-4 mr-1" />
            Disconnect
          </Button>
        </div>
      </div>

      {/* Connection info */}
      {connection.lastError && (
        <Alert variant="warning" className="mb-6">
          <AlertCircle />
          <AlertDescription>{connection.lastError}</AlertDescription>
        </Alert>
      )}

      {/* Linked Accounts */}
      <div className="mb-6">
        <h2 className="text-lg font-semibold text-foreground mb-3">
          Linked Accounts ({connection.linkedAccounts.length})
        </h2>
        {connection.linkedAccounts.length === 0 ? (
          <Card className="border-border bg-card">
            <CardContent className="p-6 text-center text-muted-foreground">
              No linked accounts found for this connection.
            </CardContent>
          </Card>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {connection.linkedAccounts.map((account) => (
              <Card
                key={account.linkedAccountId}
                className="border-border bg-card"
              >
                <CardContent className="p-4">
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex items-center gap-2">
                      <CreditCard className="w-4 h-4 text-muted-foreground" />
                      <p className="font-medium text-foreground">{account.name}</p>
                    </div>
                    <Badge variant="outline" className="text-xs">
                      {account.status}
                    </Badge>
                  </div>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <p>
                      <span className="text-muted-foreground">Type:</span>{' '}
                      {account.accountType}
                      {account.accountSubtype ? ` / ${account.accountSubtype}` : ''}
                    </p>
                    <p>
                      <span className="text-muted-foreground">Currency:</span> {account.currency}
                    </p>
                    {account.last4 && (
                      <p>
                        <span className="text-muted-foreground">Last 4:</span> ****{account.last4}
                      </p>
                    )}
                    <p>
                      <span className="text-muted-foreground">Last synced:</span>{' '}
                      {formatDateTime(account.lastSyncedAt)}
                    </p>
                  </div>
                  {account.lastError && (
                    <p className="mt-2 text-xs text-destructive">{account.lastError}</p>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Transactions */}
      <div>
        <h2 className="text-lg font-semibold text-foreground mb-3">Transactions</h2>
        <Card>
          <CardContent className="p-4">
            <div className="rounded-md border border-border overflow-hidden">
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
        <DialogContent className="max-w-[500px] max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Transaction Attachments</DialogTitle>
          </DialogHeader>
          {attachmentsLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="w-5 h-5 animate-spin text-muted-foreground" />
              <span className="ml-2 text-sm text-muted-foreground">Loading...</span>
            </div>
          ) : attachments.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              No attachments found for this transaction.
            </p>
          ) : (
            <div className="space-y-3">
              {attachments.map((att) => (
                <div
                  key={att.attachmentId}
                  className="flex items-center justify-between p-3 border border-border rounded-md"
                >
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-foreground truncate">
                      {att.fileName}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {att.contentType} &middot; {(att.fileSizeBytes / 1024).toFixed(1)} KB
                    </p>
                  </div>
                  <div className="flex items-center gap-2 ml-3">
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          aria-label={`Delete ${att.fileName}`}
                          onClick={() => setPendingDeleteAttachmentId(att.attachmentId)}
                          className="text-destructive hover:bg-destructive/10"
                        >
                          <Trash2 className="w-4 h-4" />
                        </Button>
                      </TooltipTrigger>
                      <TooltipContent>Delete attachment</TooltipContent>
                    </Tooltip>
                  </div>
                </div>
              ))}
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Disconnect confirmation */}
      <AlertDialog open={confirmDisconnectOpen} onOpenChange={setConfirmDisconnectOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Disconnect account?</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to disconnect {connection.institutionName}?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => void handleDisconnect()}>
              Disconnect
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete attachment confirmation */}
      <AlertDialog
        open={!!pendingDeleteAttachmentId}
        onOpenChange={(open) => {
          if (!open) setPendingDeleteAttachmentId(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete attachment?</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete this attachment?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                if (pendingDeleteAttachmentId) void handleDeleteAttachment(pendingDeleteAttachmentId);
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
