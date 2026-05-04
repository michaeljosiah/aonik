import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';

import { CreateTransactionDialog } from './CreateTransactionDialog';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  ArrowLeft,
  FileUp,
  Landmark,
  Paperclip,
  Plus,
  Trash2,
} from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import { accountService } from '@/services/accountService';
import type {
  AccountResponse,
  AccountTransactionResponse,
  AccountTransactionAttachmentResponse,
} from '@/types';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
} from '@/components/ui/data-table';

export function AccountTransactionsPage() {
  const { accountId } = useParams<{ accountId: string }>();
  const navigate = useNavigate();

  const [account, setAccount] = useState<AccountResponse | null>(null);
  const [transactions, setTransactions] = useState<AccountTransactionResponse[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [searchQuery, setSearchQuery] = useState('');
  const [showCreateTransaction, setShowCreateTransaction] = useState(false);

  // Attachment state
  const [attachmentsDialogTxId, setAttachmentsDialogTxId] = useState<string | null>(null);
  const [attachments, setAttachments] = useState<AccountTransactionAttachmentResponse[]>([]);
  const [attachmentsLoading, setAttachmentsLoading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const uploadTargetTxIdRef = useRef<string | null>(null);

  const loadAccount = useCallback(async () => {
    if (!accountId) return;
    try {
      const accounts = await accountService.listAccounts();
      const found = accounts.find((a) => a.accountId === accountId);
      setAccount(found ?? null);
    } catch {
      // Non-fatal
    }
  }, [accountId]);

  const loadTransactions = useCallback(async () => {
    if (!accountId) return;
    setLoading(true);
    try {
      const result = await accountService.listTransactions({
        accountId: accountId,
        pageNumber,
        pageSize,
      });
      setTransactions(result.items);
      setTotalCount(result.totalCount);
    } catch {
      toast.error('Failed to load transactions.');
    } finally {
      setLoading(false);
    }
  }, [accountId, pageNumber, pageSize]);

  useEffect(() => {
    loadAccount();
  }, [loadAccount]);

  useEffect(() => {
    loadTransactions();
  }, [loadTransactions]);

  const loadAttachments = useCallback(async (transactionId: string) => {
    setAttachmentsLoading(true);
    try {
      const result = await accountService.listAttachments(transactionId);
      setAttachments(result);
    } catch {
      toast.error('Failed to load attachments.');
    } finally {
      setAttachmentsLoading(false);
    }
  }, []);

  const handleUploadClick = (transactionId: string) => {
    uploadTargetTxIdRef.current = transactionId;
    fileInputRef.current?.click();
  };

  const handleFileSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const txId = uploadTargetTxIdRef.current;
    if (!file || !txId) return;

    try {
      await accountService.uploadAttachment(txId, file);
      toast.success('File uploaded successfully.');
      if (attachmentsDialogTxId === txId) {
        await loadAttachments(txId);
      }
    } catch {
      toast.error('Failed to upload file.');
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = '';
      uploadTargetTxIdRef.current = null;
    }
  };

  const handleDeleteAttachment = async (attachmentId: string) => {
    if (!window.confirm('Delete this attachment?')) return;
    try {
      await accountService.deleteAttachment(attachmentId);
      toast.success('Attachment deleted.');
      if (attachmentsDialogTxId) {
        await loadAttachments(attachmentsDialogTxId);
      }
    } catch {
      toast.error('Failed to delete attachment.');
    }
  };

  const openAttachments = (transactionId: string) => {
    setAttachmentsDialogTxId(transactionId);
    loadAttachments(transactionId);
  };

  const getRowActions = (tx: AccountTransactionResponse): DataTableAction[] => [
    {
      label: 'Upload File',
      icon: <FileUp className="w-4 h-4" />,
      onClick: () => handleUploadClick(tx.transactionId),
    },
    {
      label: 'Attachments',
      icon: <Paperclip className="w-4 h-4" />,
      onClick: () => openAttachments(tx.transactionId),
    },
  ];

  const columns: ColumnDef<AccountTransactionResponse>[] = [
    {
      id: 'occurredAt',
      header: 'Date',
      accessorFn: (row) => new Date(row.occurredAt),
      sortable: true,
      cell: (tx) => (
        <span className="text-sm text-[var(--color-text-primary)]">
          {new Date(tx.occurredAt).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })}
        </span>
      ),
    },
    {
      id: 'amount',
      header: 'Amount',
      accessorKey: 'amount',
      sortable: true,
      cell: (tx) => {
        const isDebit = tx.amount < 0;
        return (
          <span className={`text-sm font-medium ${isDebit ? 'text-[var(--color-error)]' : 'text-[var(--color-success)]'}`}>
            {isDebit ? '' : '+'}{tx.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })} {tx.currency}
          </span>
        );
      },
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
      cell: (tx) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{tx.description || '—'}</span>
      ),
    },
    {
      id: 'reference',
      header: 'Reference',
      accessorKey: 'reference',
      cell: (tx) => (
        <span className="text-sm text-[var(--color-text-tertiary)]">{tx.reference || '—'}</span>
      ),
    },
    {
      id: 'reconciliationStatus',
      header: 'Recon Status',
      accessorKey: 'reconciliationStatus',
      sortable: true,
      cell: (tx) => {
        const matched = tx.reconciliationStatus === 'Matched';
        const style = matched
          ? 'bg-[var(--color-success-light)] text-[var(--color-success)]'
          : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]';
        return (
          <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style}`}>
            {tx.reconciliationStatus}
          </span>
        );
      },
    },
  ];

  const filteredTransactions = searchQuery
    ? transactions.filter((tx) => {
        const q = searchQuery.toLowerCase();
        return (
          (tx.counterparty ?? '').toLowerCase().includes(q) ||
          (tx.description ?? '').toLowerCase().includes(q) ||
          (tx.reference ?? '').toLowerCase().includes(q)
        );
      })
    : transactions;
  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate('/accounts')}>
            <ArrowLeft className="w-4 h-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
              {account?.maskedIdentifier ?? 'Account'} Transactions
            </h1>
            <p className="text-[var(--color-text-secondary)]">
              {account ? `${account.accountType} — ${account.verificationStatus === 'Verified' ? 'Linked' : 'Manual'}` : ''}
            </p>
          </div>
        </div>
        <Button onClick={() => setShowCreateTransaction(true)} className="rounded-sm">
          <Plus className="w-4 h-4 mr-2" />
          Add Transaction
        </Button>
      </div>

      <Card>
        <CardContent className="p-4">
          <DataTableHeader
            searchValue={searchQuery}
            onSearchChange={setSearchQuery}
            searchPlaceholder="Search transactions"
            className="px-0 border-b-0"
          />

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <DataTable
              data={filteredTransactions}
              columns={columns}
              getRowId={(t) => t.transactionId}
              loading={loading}
              loadingMessage="Loading transactions..."
              emptyIcon={<Landmark className="w-12 h-12" />}
              emptyTitle="No transactions yet"
              emptyDescription="Add a transaction manually or sync from a linked provider."
              rowActions={(tx) => <DataTableRowActions actions={getRowActions(tx)} />}
              rowActionsPosition="start"
            />
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={(s) => { setPageSize(s); setPageNumber(1); }}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>

      {/* Hidden file input for uploads */}
      <input
        ref={fileInputRef}
        type="file"
        className="hidden"
        accept=".pdf,.jpg,.jpeg,.png,.csv,.xlsx,.doc,.docx"
        onChange={handleFileSelected}
      />

      {/* Attachments dialog */}
      <Dialog open={!!attachmentsDialogTxId} onOpenChange={(open) => { if (!open) setAttachmentsDialogTxId(null); }}>
        <DialogContent className="max-w-[32rem]">
          <DialogHeader>
            <DialogTitle>Attachments</DialogTitle>
          </DialogHeader>
          {attachmentsLoading ? (
            <p className="text-sm text-[var(--color-text-secondary)] py-4">Loading...</p>
          ) : attachments.length === 0 ? (
            <p className="text-sm text-[var(--color-text-secondary)] py-4">No attachments. Use "Upload File" to add one.</p>
          ) : (
            <div className="space-y-2 max-h-64 overflow-auto">
              {attachments.map((att) => (
                <div key={att.attachmentId} className="flex items-center justify-between p-2 rounded border border-[var(--color-border-light)]">
                  <div className="flex items-center gap-2 min-w-0">
                    <Paperclip className="w-4 h-4 shrink-0 text-[var(--color-text-tertiary)]" />
                    <a
                      href={att.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-sm text-[var(--color-primary)] hover:underline truncate"
                    >
                      {att.fileName}
                    </a>
                    <span className="text-xs text-[var(--color-text-tertiary)] shrink-0">
                      {(att.fileSizeBytes / 1024).toFixed(0)} KB
                    </span>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleDeleteAttachment(att.attachmentId)}
                  >
                    <Trash2 className="w-4 h-4 text-[var(--color-error)]" />
                  </Button>
                </div>
              ))}
            </div>
          )}
        </DialogContent>
      </Dialog>

      <CreateTransactionDialog
        open={showCreateTransaction}
        onOpenChange={setShowCreateTransaction}
        onSuccess={loadTransactions}
        preselectedAccountId={accountId}
      />
    </div>
  );
}
