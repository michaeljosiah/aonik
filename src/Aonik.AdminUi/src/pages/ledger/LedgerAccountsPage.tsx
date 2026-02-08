import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { AlertCircle, Landmark, Plus } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { documentService } from '@/services/documentService';
import { identityService } from '@/services/identityService';
import { ledgerService } from '@/services/ledgerService';
import type { CreateLedgerAccountRequest, DocumentListItem, LedgerAccountSummary, LedgerSummary, PagedResult } from '@/types';

const accountTypes = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];

export function LedgerAccountsPage() {
  const [ledgers, setLedgers] = useState<LedgerSummary[]>([]);
  const [accounts, setAccounts] = useState<LedgerAccountSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [ledgerFilter, setLedgerFilter] = useState<string>('');
  const [formState, setFormState] = useState<CreateLedgerAccountRequest>({
    ledgerId: '',
    name: '',
    code: '',
    accountType: 'Asset',
  });
  const [isSaving, setIsSaving] = useState(false);
  const [ownerPartyId, setOwnerPartyId] = useState<string>('');
  const [selectedAccountId, setSelectedAccountId] = useState<string>('');
  const [documentFile, setDocumentFile] = useState<File | null>(null);
  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  const loadLedgers = useCallback(async () => {
    try {
      const response = await ledgerService.listLedgers();
      setLedgers(response);
      if (!ledgerFilter && response.length > 0) {
        setLedgerFilter(response[0].id);
      }
      if (!formState.ledgerId && response.length > 0) {
        setFormState((prev) => ({ ...prev, ledgerId: response[0].id }));
      }
    } catch (err: unknown) {
      console.error('Failed to load ledgers:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load ledgers.');
    }
  }, [formState.ledgerId, ledgerFilter]);

  const loadOwnerParty = useCallback(async () => {
    try {
      const userInfo = await identityService.getUserInfo();
      setOwnerPartyId(userInfo.partyId);
    } catch (err: unknown) {
      console.error('Failed to load user info:', err);
    }
  }, []);

  const loadDocuments = useCallback(async (accountId: string) => {
    try {
      const response: PagedResult<DocumentListItem> = await documentService.list({
        relatedEntityType: 'LedgerAccount',
        relatedEntityId: accountId,
        pageSize: 10,
        pageNumber: 1,
      });
      setDocuments(response.items);
    } catch (err: unknown) {
      console.error('Failed to load account documents:', err);
    }
  }, []);

  const loadAccounts = useCallback(async (ledgerId?: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await ledgerService.listAccounts(ledgerId);
      setAccounts(response);
    } catch (err: unknown) {
      console.error('Failed to load ledger accounts:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load ledger accounts.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadLedgers();
    loadOwnerParty();
  }, [loadLedgers, loadOwnerParty]);

  useEffect(() => {
    if (ledgerFilter) {
      loadAccounts(ledgerFilter);
    }
  }, [ledgerFilter, loadAccounts]);

  useEffect(() => {
    if (accounts.length > 0 && !selectedAccountId) {
      setSelectedAccountId(accounts[0].id);
    }
  }, [accounts, selectedAccountId]);

  useEffect(() => {
    if (selectedAccountId) {
      loadDocuments(selectedAccountId);
    }
  }, [loadDocuments, selectedAccountId]);

  const handleCreateAccount = useCallback(async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!formState.ledgerId || !formState.name.trim() || !formState.code.trim()) {
      setError('Ledger, name, and code are required.');
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      await ledgerService.createAccount({
        ledgerId: formState.ledgerId,
        name: formState.name.trim(),
        code: formState.code.trim(),
        accountType: formState.accountType,
      });
      await loadAccounts(ledgerFilter || formState.ledgerId);
      setFormState((prev) => ({ ...prev, name: '', code: '' }));
    } catch (err: unknown) {
      console.error('Failed to create ledger account:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to create ledger account.');
    } finally {
      setIsSaving(false);
    }
  }, [formState, ledgerFilter, loadAccounts]);

  const ledgerOptions = useMemo(() => ledgers.map((ledger) => ({
    value: ledger.id,
    label: `${ledger.baseCurrency} • ${ledger.id.slice(0, 8)}`,
  })), [ledgers]);

  const accountOptions = useMemo(() => accounts.map((account) => ({
    value: account.id,
    label: `${account.code} • ${account.name}`,
  })), [accounts]);

  const handleUploadDocument = useCallback(async () => {
    if (!ownerPartyId) {
      setError('Owner party ID is required to upload documents.');
      return;
    }
    if (!selectedAccountId) {
      setError('Select a ledger account to attach this document.');
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
        documentType: 'LedgerAccountAttachment',
        status: undefined,
        issuedOn: undefined,
        expiresOn: undefined,
        issuerName: undefined,
        countryCode: undefined,
        referenceNumber: undefined,
        tags: ['ledger-account'],
        attributesJson: undefined,
      });

      await documentService.uploadFile(document.documentId, {
        file: documentFile,
      });

      await documentService.addUsage(document.documentId, {
        ownerPartyId,
        purpose: 'LedgerAccountAttachment',
        relatedEntityType: 'LedgerAccount',
        relatedEntityId: selectedAccountId,
        status: 'Active',
        notes: 'Ledger account document',
      });

      setDocumentFile(null);
      await loadDocuments(selectedAccountId);
    } catch (err: unknown) {
      console.error('Failed to upload ledger account document:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to upload document.');
    } finally {
      setIsUploading(false);
    }
  }, [documentFile, loadDocuments, ownerPartyId, selectedAccountId]);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Ledger', href: '/ledger' },
          { label: 'Accounts', icon: <Landmark className="w-3.5 h-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Ledger Accounts</h1>
          <p className="text-[var(--color-text-secondary)]">Create and manage your chart of accounts.</p>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={() => loadAccounts(ledgerFilter)}>
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
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Account list</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Accounts by ledger.</p>
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
                <div className="p-6 text-sm text-[var(--color-text-secondary)]">Loading accounts...</div>
              ) : accounts.length === 0 ? (
                <div className="p-6 text-sm text-[var(--color-text-secondary)]">No accounts yet.</div>
              ) : (
                <table className="w-full">
                  <thead>
                    <tr className="bg-[var(--color-surface-inset)]/60 border-b border-[var(--color-border-light)]">
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Account</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Type</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Code</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Currency</th>
                    </tr>
                  </thead>
                  <tbody>
                    {accounts.map((account) => (
                      <tr key={account.id} className="border-b border-[var(--color-border-light)]">
                        <td className="px-4 py-3">
                          <p className="text-sm font-medium text-[var(--color-text-primary)]">{account.name}</p>
                          <p className="text-xs text-[var(--color-text-tertiary)] font-mono">{account.id.slice(0, 8)}</p>
                        </td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{account.accountType}</td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)] font-mono">{account.code}</td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{account.currency || '—'}</td>
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
                <Landmark className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Create account</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Add a new ledger account.</p>
              </div>
            </div>

            <form onSubmit={handleCreateAccount} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="account-ledger">Ledger</Label>
                <Select
                  value={formState.ledgerId}
                  onValueChange={(value) => setFormState((prev) => ({ ...prev, ledgerId: value }))}
                >
                  <SelectTrigger id="account-ledger" className="h-9 rounded-sm">
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
                <Label htmlFor="account-name">Account name</Label>
                <Input
                  id="account-name"
                  value={formState.name}
                  onChange={(event) => setFormState((prev) => ({ ...prev, name: event.target.value }))}
                  placeholder="Cash on hand"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="account-code">Account code</Label>
                <Input
                  id="account-code"
                  value={formState.code}
                  onChange={(event) => setFormState((prev) => ({ ...prev, code: event.target.value }))}
                  placeholder="1000"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="account-type">Account type</Label>
                <Select
                  value={formState.accountType}
                  onValueChange={(value) => setFormState((prev) => ({ ...prev, accountType: value }))}
                >
                  <SelectTrigger id="account-type" className="h-9 rounded-sm">
                    <SelectValue placeholder="Select type" />
                  </SelectTrigger>
                  <SelectContent>
                    {accountTypes.map((type) => (
                      <SelectItem key={type} value={type}>
                        {type}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <Button type="submit" className="w-full rounded-sm" disabled={isSaving}>
                <Plus className="w-4 h-4 mr-2" />
                {isSaving ? 'Saving...' : 'Create account'}
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
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Account documents</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Files linked to the selected account.</p>
              </div>
              <div className="w-56">
                <Select value={selectedAccountId} onValueChange={setSelectedAccountId}>
                  <SelectTrigger className="h-9 rounded-sm">
                    <SelectValue placeholder="Select account" />
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
                <Landmark className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Upload document</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Attach evidence to a ledger account.</p>
              </div>
            </div>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="ledger-account-doc-file">Document file</Label>
                <Input
                  id="ledger-account-doc-file"
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
