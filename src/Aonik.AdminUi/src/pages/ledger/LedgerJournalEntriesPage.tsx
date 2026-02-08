import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { AlertCircle, ClipboardList, Plus } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { documentService } from '@/services/documentService';
import { identityService } from '@/services/identityService';
import { ledgerService } from '@/services/ledgerService';
import type { AddJournalEntryRequest, DocumentListItem, JournalEntryResponse, LedgerAccountSummary, LedgerSummary, PagedResult } from '@/types';

export function LedgerJournalEntriesPage() {
  const [ledgers, setLedgers] = useState<LedgerSummary[]>([]);
  const [accounts, setAccounts] = useState<LedgerAccountSummary[]>([]);
  const [entries, setEntries] = useState<JournalEntryResponse[]>([]);
  const [ledgerFilter, setLedgerFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [ownerPartyId, setOwnerPartyId] = useState<string>('');
  const [selectedEntryId, setSelectedEntryId] = useState<string>('');
  const [documentFile, setDocumentFile] = useState<File | null>(null);
  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  const [formState, setFormState] = useState({
    ledgerId: '',
    reference: '',
    description: '',
    debitAccountId: '',
    creditAccountId: '',
    amount: '',
    currency: '',
  });

  const loadLedgers = useCallback(async () => {
    try {
      const response = await ledgerService.listLedgers();
      setLedgers(response);
      if (!ledgerFilter && response.length > 0) {
        setLedgerFilter(response[0].id);
      }
      if (!formState.ledgerId && response.length > 0) {
        const defaultLedgerId = response[0].id;
        setFormState((prev) => ({
          ...prev,
          ledgerId: defaultLedgerId,
          currency: response[0].baseCurrency,
        }));
      }
    } catch (err: unknown) {
      console.error('Failed to load ledgers:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load ledgers.');
    }
  }, [formState.ledgerId, ledgerFilter]);

  const loadAccounts = useCallback(async (ledgerId: string) => {
    try {
      const response = await ledgerService.listAccounts(ledgerId);
      setAccounts(response);
    } catch (err: unknown) {
      console.error('Failed to load accounts:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load accounts.');
    }
  }, []);

  const loadEntries = useCallback(async (ledgerId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await ledgerService.listJournalEntries(ledgerId);
      setEntries(response);
    } catch (err: unknown) {
      console.error('Failed to load journal entries:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load journal entries.');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadOwnerParty = useCallback(async () => {
    try {
      const userInfo = await identityService.getUserInfo();
      setOwnerPartyId(userInfo.partyId);
    } catch (err: unknown) {
      console.error('Failed to load user info:', err);
    }
  }, []);

  const loadDocuments = useCallback(async (entryId: string) => {
    try {
      const response: PagedResult<DocumentListItem> = await documentService.list({
        relatedEntityType: 'JournalEntry',
        relatedEntityId: entryId,
        pageSize: 10,
        pageNumber: 1,
      });
      setDocuments(response.items);
    } catch (err: unknown) {
      console.error('Failed to load journal entry documents:', err);
    }
  }, []);

  useEffect(() => {
    loadLedgers();
    loadOwnerParty();
  }, [loadLedgers, loadOwnerParty]);

  useEffect(() => {
    if (ledgerFilter) {
      loadAccounts(ledgerFilter);
      loadEntries(ledgerFilter);
      const ledger = ledgers.find((item) => item.id === ledgerFilter);
      if (ledger) {
        setFormState((prev) => ({ ...prev, ledgerId: ledgerFilter, currency: ledger.baseCurrency }));
      }
    }
  }, [ledgerFilter, ledgers, loadAccounts, loadEntries]);

  useEffect(() => {
    if (entries.length > 0 && !selectedEntryId) {
      setSelectedEntryId(entries[0].id);
    }
  }, [entries, selectedEntryId]);

  useEffect(() => {
    if (selectedEntryId) {
      loadDocuments(selectedEntryId);
    }
  }, [loadDocuments, selectedEntryId]);

  const handleCreateEntry = useCallback(async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!formState.ledgerId || !formState.debitAccountId || !formState.creditAccountId || !formState.amount) {
      setError('Ledger, debit account, credit account, and amount are required.');
      return;
    }

    const amountValue = Number(formState.amount);
    if (Number.isNaN(amountValue) || amountValue <= 0) {
      setError('Amount must be greater than zero.');
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const payload: AddJournalEntryRequest = {
        ledgerId: formState.ledgerId,
        reference: formState.reference || null,
        description: formState.description || null,
        lines: [
          {
            accountId: formState.debitAccountId,
            direction: 'Debit',
            amount: amountValue,
            currency: formState.currency || 'USD',
            narration: 'Debit line',
          },
          {
            accountId: formState.creditAccountId,
            direction: 'Credit',
            amount: amountValue,
            currency: formState.currency || 'USD',
            narration: 'Credit line',
          },
        ],
      };

      await ledgerService.addJournalEntry(payload);
      await loadEntries(formState.ledgerId);
      setFormState((prev) => ({
        ...prev,
        reference: '',
        description: '',
        debitAccountId: '',
        creditAccountId: '',
        amount: '',
      }));
    } catch (err: unknown) {
      console.error('Failed to add journal entry:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to add journal entry.');
    } finally {
      setIsSaving(false);
    }
  }, [formState, loadEntries]);

  const accountOptions = useMemo(() => accounts.map((account) => ({
    value: account.id,
    label: `${account.code} • ${account.name}`,
  })), [accounts]);

  const entryOptions = useMemo(() => entries.map((entry) => ({
    value: entry.id,
    label: `${entry.reference ?? 'Manual'} · ${entry.id.slice(0, 8)}`,
  })), [entries]);

  const ledgerOptions = useMemo(() => ledgers.map((ledger) => ({
    value: ledger.id,
    label: `${ledger.baseCurrency} • ${ledger.id.slice(0, 8)}`,
  })), [ledgers]);

  const entryRows = useMemo(() => entries.map((entry) => {
    const debitTotal = entry.lines
      .filter((line) => line.direction.toLowerCase() === 'debit')
      .reduce((sum, line) => sum + line.amount, 0);
    return {
      ...entry,
      debitTotal,
      createdLabel: new Date(entry.entryUtc).toLocaleString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }),
    };
  }), [entries]);

  const handleUploadDocument = useCallback(async () => {
    if (!ownerPartyId) {
      setError('Owner party ID is required to upload documents.');
      return;
    }
    if (!selectedEntryId) {
      setError('Select a journal entry to attach this document.');
      return;
    }
    if (!documentFile) {
      setError('Select a file to upload.');
      return;
    }
    setIsUploading(true);
    setError(null);
    try {
      const document = await documentService.create({
        ownerPartyId,
        documentType: 'JournalEntryAttachment',
        status: undefined,
        issuedOn: undefined,
        expiresOn: undefined,
        issuerName: undefined,
        countryCode: undefined,
        referenceNumber: undefined,
        tags: ['journal-entry'],
        attributesJson: undefined,
      });

      await documentService.uploadFile(document.documentId, {
        file: documentFile,
      });

      await documentService.addUsage(document.documentId, {
        ownerPartyId,
        purpose: 'JournalEntryAttachment',
        relatedEntityType: 'JournalEntry',
        relatedEntityId: selectedEntryId,
        status: 'Active',
        notes: 'Journal entry document',
      });

      setDocumentFile(null);
      await loadDocuments(selectedEntryId);
    } catch (err: unknown) {
      console.error('Failed to upload journal entry document:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to upload document.');
    } finally {
      setIsUploading(false);
    }
  }, [documentFile, loadDocuments, ownerPartyId, selectedEntryId]);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Ledger', href: '/ledger' },
          { label: 'Journal Entries', icon: <ClipboardList className="w-3.5 h-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Journal Entries</h1>
          <p className="text-[var(--color-text-secondary)]">Post balanced transactions against ledger accounts.</p>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={() => ledgerFilter && loadEntries(ledgerFilter)}>
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6 lg:grid-cols-[2fr,1fr]">
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Entry list</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Recent postings for the ledger.</p>
              </div>
              <div className="w-48">
                <Select value={ledgerFilter} onValueChange={setLedgerFilter}>
                  <SelectTrigger className="h-9 rounded-sm">
                    <SelectValue placeholder="Select ledger" />
                  </SelectTrigger>
                  <SelectContent>
                    {ledgerOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="border border-[var(--color-border-light)] rounded-md overflow-hidden">
              {loading ? (
                <div className="p-6 text-sm text-[var(--color-text-secondary)]">Loading entries...</div>
              ) : entryRows.length === 0 ? (
                <div className="p-6 text-sm text-[var(--color-text-secondary)]">No journal entries yet.</div>
              ) : (
                <table className="w-full">
                  <thead>
                    <tr className="bg-[var(--color-surface-inset)]/60 border-b border-[var(--color-border-light)]">
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Reference</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Status</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Amount</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Posted</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entryRows.map((entry) => (
                      <tr key={entry.id} className="border-b border-[var(--color-border-light)]">
                        <td className="px-4 py-3">
                          <p className="text-sm font-medium text-[var(--color-text-primary)]">{entry.reference || 'Manual entry'}</p>
                          <p className="text-xs text-[var(--color-text-tertiary)]">{entry.description || '—'}</p>
                        </td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{entry.status}</td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">
                          {entry.debitTotal.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{entry.createdLabel}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-9 h-9 rounded-full bg-[var(--color-surface-inset)] flex items-center justify-center text-[var(--color-text-secondary)]">
                <ClipboardList className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Add transaction</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Create a balanced journal entry.</p>
              </div>
            </div>

            <form onSubmit={handleCreateEntry} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="entry-ledger">Ledger</Label>
                <Select
                  value={formState.ledgerId}
                  onValueChange={(value) => setFormState((prev) => ({ ...prev, ledgerId: value }))}
                >
                  <SelectTrigger id="entry-ledger" className="h-9 rounded-sm">
                    <SelectValue placeholder="Select ledger" />
                  </SelectTrigger>
                  <SelectContent>
                    {ledgerOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="entry-reference">Reference</Label>
                <Input
                  id="entry-reference"
                  value={formState.reference}
                  onChange={(event) => setFormState((prev) => ({ ...prev, reference: event.target.value }))}
                  placeholder="REF-1001"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="entry-description">Description</Label>
                <Textarea
                  id="entry-description"
                  value={formState.description}
                  onChange={(event) => setFormState((prev) => ({ ...prev, description: event.target.value }))}
                  placeholder="Brief description of the transaction"
                  rows={3}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="entry-debit">Debit account</Label>
                <Select
                  value={formState.debitAccountId}
                  onValueChange={(value) => setFormState((prev) => ({ ...prev, debitAccountId: value }))}
                >
                  <SelectTrigger id="entry-debit" className="h-9 rounded-sm">
                    <SelectValue placeholder="Select debit account" />
                  </SelectTrigger>
                  <SelectContent>
                    {accountOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="entry-credit">Credit account</Label>
                <Select
                  value={formState.creditAccountId}
                  onValueChange={(value) => setFormState((prev) => ({ ...prev, creditAccountId: value }))}
                >
                  <SelectTrigger id="entry-credit" className="h-9 rounded-sm">
                    <SelectValue placeholder="Select credit account" />
                  </SelectTrigger>
                  <SelectContent>
                    {accountOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-4 grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="entry-amount">Amount</Label>
                  <Input
                    id="entry-amount"
                    type="number"
                    min="0"
                    step="0.01"
                    value={formState.amount}
                    onChange={(event) => setFormState((prev) => ({ ...prev, amount: event.target.value }))}
                    placeholder="0.00"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="entry-currency">Currency</Label>
                  <Input
                    id="entry-currency"
                    value={formState.currency}
                    onChange={(event) => setFormState((prev) => ({ ...prev, currency: event.target.value }))}
                    placeholder="USD"
                    maxLength={3}
                  />
                </div>
              </div>
              <Button type="submit" className="w-full rounded-sm" disabled={isSaving}>
                <Plus className="w-4 h-4 mr-2" />
                {isSaving ? 'Posting...' : 'Post transaction'}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 mt-6 lg:grid-cols-[1fr,1fr]">
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Entry documents</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Files linked to the selected journal entry.</p>
              </div>
              <div className="w-60">
                <Select value={selectedEntryId} onValueChange={setSelectedEntryId}>
                  <SelectTrigger className="h-9 rounded-sm">
                    <SelectValue placeholder="Select entry" />
                  </SelectTrigger>
                  <SelectContent>
                    {entryOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            {documents.length === 0 ? (
              <p className="text-sm text-[var(--color-text-tertiary)]">No documents attached.</p>
            ) : (
              <div className="space-y-3">
                {documents.map((doc) => (
                  <div key={doc.documentId} className="flex items-center justify-between border-b border-[var(--color-border-light)] pb-3 last:border-b-0">
                    <div>
                      <p className="text-sm font-medium text-[var(--color-text-primary)]">{doc.documentType}</p>
                      <p className="text-xs text-[var(--color-text-tertiary)]">Status: {doc.status}</p>
                    </div>
                    <div className="text-xs text-[var(--color-text-tertiary)]">
                      {new Date(doc.createdAt).toLocaleDateString('en-US')}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-9 h-9 rounded-full bg-[var(--color-surface-inset)] flex items-center justify-center text-[var(--color-text-secondary)]">
                <ClipboardList className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Upload document</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Attach evidence to a journal entry.</p>
              </div>
            </div>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="journal-entry-doc-file">Document file</Label>
                <Input
                  id="journal-entry-doc-file"
                  type="file"
                  onChange={(event) => setDocumentFile(event.target.files?.[0] ?? null)}
                />
              </div>
              <Button onClick={handleUploadDocument} disabled={isUploading} className="w-full rounded-sm">
                {isUploading ? 'Uploading...' : 'Upload document'}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
