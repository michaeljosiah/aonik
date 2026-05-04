import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { AlertCircle, BookOpen, Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { documentService } from '@/services/documentService';
import { identityService } from '@/services/identityService';
import { ledgerService } from '@/services/ledgerService';
import type { CreateLedgerRequest, DocumentListItem, LedgerSummary, PagedResult } from '@/types';

export function LedgerOverviewPage() {
  const [ledgers, setLedgers] = useState<LedgerSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [baseCurrency, setBaseCurrency] = useState('USD');
  const [isSaving, setIsSaving] = useState(false);
  const [ownerPartyId, setOwnerPartyId] = useState<string>('');
  const [selectedLedgerId, setSelectedLedgerId] = useState<string>('');
  const [documentFile, setDocumentFile] = useState<File | null>(null);
  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  const loadLedgers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await ledgerService.listLedgers();
      setLedgers(response);
      if (!selectedLedgerId && response.length > 0) {
        setSelectedLedgerId(response[0].id);
      }
    } catch (err: unknown) {
      console.error('Failed to load ledgers:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load ledgers.');
    } finally {
      setLoading(false);
    }
  }, [selectedLedgerId]);

  const loadOwnerParty = useCallback(async () => {
    try {
      const userInfo = await identityService.getUserInfo();
      setOwnerPartyId(userInfo.partyId);
    } catch (err: unknown) {
      console.error('Failed to load user info:', err);
    }
  }, []);

  const loadDocuments = useCallback(async (ledgerId: string) => {
    try {
      const response: PagedResult<DocumentListItem> = await documentService.list({
        relatedEntityType: 'Ledger',
        relatedEntityId: ledgerId,
        pageSize: 10,
        pageNumber: 1,
      });
      setDocuments(response.items);
    } catch (err: unknown) {
      console.error('Failed to load ledger documents:', err);
    }
  }, []);

  useEffect(() => {
    loadLedgers();
    loadOwnerParty();
  }, [loadLedgers, loadOwnerParty]);

  useEffect(() => {
    if (selectedLedgerId) {
      loadDocuments(selectedLedgerId);
    }
  }, [selectedLedgerId, loadDocuments]);

  const handleCreateLedger = useCallback(async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!baseCurrency.trim()) {
      setError('Base currency is required.');
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      const payload: CreateLedgerRequest = {
        baseCurrency: baseCurrency.trim().toUpperCase(),
      };
      await ledgerService.createLedger(payload);
      await loadLedgers();
    } catch (err: unknown) {
      console.error('Failed to create ledger:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to create ledger.');
    } finally {
      setIsSaving(false);
    }
  }, [baseCurrency, loadLedgers]);

  const ledgerRows = useMemo(() => {
    return ledgers.map((ledger) => ({
      ...ledger,
      createdLabel: new Date(ledger.createdUtc).toLocaleString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }),
    }));
  }, [ledgers]);

  const handleUploadDocument = useCallback(async () => {
    if (!ownerPartyId) {
      setError('Owner party ID is required to upload documents.');
      return;
    }
    if (!selectedLedgerId) {
      setError('Select a ledger to attach this document.');
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
        documentType: 'LedgerAttachment',
        status: undefined,
        issuedOn: undefined,
        expiresOn: undefined,
        issuerName: undefined,
        countryCode: undefined,
        referenceNumber: undefined,
        tags: ['ledger'],
        attributesJson: undefined,
      });

      await documentService.uploadFile(document.documentId, {
        file: documentFile,
      });

      await documentService.addUsage(document.documentId, {
        ownerPartyId,
        purpose: 'LedgerAttachment',
        relatedEntityType: 'Ledger',
        relatedEntityId: selectedLedgerId,
        status: 'Active',
        notes: 'Ledger document',
      });

      setDocumentFile(null);
      await loadDocuments(selectedLedgerId);
    } catch (err: unknown) {
      console.error('Failed to upload ledger document:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Unable to upload document.');
    } finally {
      setIsUploading(false);
    }
  }, [documentFile, loadDocuments, ownerPartyId, selectedLedgerId]);

  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Ledgers</h1>
          <p className="text-[var(--color-text-secondary)]">
            Maintain the ledger books that anchor your financial truth.
          </p>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadLedgers}>
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
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Ledger list</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">All ledgers available to this tenant.</p>
              </div>
              <span className="text-xs text-[var(--color-text-tertiary)]">{ledgerRows.length} total</span>
            </div>

            <div className="border border-[var(--color-border-light)] rounded-md overflow-hidden">
              {loading ? (
                <div className="p-6 text-sm text-[var(--color-text-secondary)]">Loading ledgers...</div>
              ) : ledgerRows.length === 0 ? (
                <div className="p-6 text-sm text-[var(--color-text-secondary)]">No ledgers created yet.</div>
              ) : (
                <table className="w-full">
                  <thead>
                    <tr className="bg-[var(--color-surface-inset)]/60 border-b border-[var(--color-border-light)]">
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Ledger ID</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Base currency</th>
                      <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Created</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ledgerRows.map((ledger) => (
                      <tr key={ledger.id} className="border-b border-[var(--color-border-light)]">
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)] font-mono">{ledger.id}</td>
                        <td className="px-4 py-3 text-sm font-medium text-[var(--color-text-primary)]">{ledger.baseCurrency}</td>
                        <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{ledger.createdLabel}</td>
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
                <BookOpen className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Create ledger</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Add a new ledger book for this tenant.</p>
              </div>
            </div>

            <form onSubmit={handleCreateLedger} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="ledger-base-currency">Base currency</Label>
                <Input
                  id="ledger-base-currency"
                  value={baseCurrency}
                  onChange={(event) => setBaseCurrency(event.target.value)}
                  placeholder="USD"
                  maxLength={3}
                />
              </div>
              <Button type="submit" className="w-full rounded-sm" disabled={isSaving}>
                <Plus className="w-4 h-4 mr-2" />
                {isSaving ? 'Creating...' : 'Create ledger'}
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
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Ledger documents</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Files linked to the selected ledger.</p>
              </div>
              <select
                value={selectedLedgerId}
                onChange={(event) => setSelectedLedgerId(event.target.value)}
                className="h-9 rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 text-sm"
              >
                {ledgerRows.map((ledger) => (
                  <option key={ledger.id} value={ledger.id}>
                    {ledger.baseCurrency} · {ledger.id.slice(0, 8)}
                  </option>
                ))}
              </select>
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
                <BookOpen className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">Upload document</h2>
                <p className="text-sm text-[var(--color-text-secondary)]">Attach evidence to a ledger.</p>
              </div>
            </div>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="ledger-doc-file">Document file</Label>
                <Input
                  id="ledger-doc-file"
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
