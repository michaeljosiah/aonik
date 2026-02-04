import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { DataTablePagination } from '@/components/ui/data-table';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { AlertCircle, FileText, Plus, RefreshCw, Search } from 'lucide-react';
import { documentService } from '@/services/documentService';
import type { DocumentListItem, PagedResult } from '@/types';

const statusStyles: Record<string, string> = {
  Draft: 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]',
  Pending: 'bg-[var(--color-warning-light)] text-[var(--color-warning)]',
  Approved: 'bg-[var(--color-success-light)] text-[var(--color-success)]',
  Rejected: 'bg-[var(--color-error-light)] text-[var(--color-error)]',
  Expired: 'bg-[var(--color-pending-light)] text-[var(--color-pending)]',
};

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const isExpiringSoon = (expiresOn?: string | null) => {
  if (!expiresOn) return false;
  const expiresAt = new Date(expiresOn).getTime();
  if (Number.isNaN(expiresAt)) return false;
  const now = Date.now();
  const daysRemaining = (expiresAt - now) / (1000 * 60 * 60 * 24);
  return daysRemaining >= 0 && daysRemaining <= 30;
};

export function DocumentsListPage() {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [ownerFilter, setOwnerFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);

  const loadDocuments = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<DocumentListItem> = await documentService.list({
        pageNumber,
        pageSize,
        search: searchQuery || undefined,
        status: statusFilter || undefined,
        documentType: typeFilter || undefined,
        ownerPartyId: ownerFilter || undefined,
      });
      setDocuments(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load documents:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load documents. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, searchQuery, statusFilter, typeFilter, ownerFilter]);

  useEffect(() => {
    loadDocuments();
  }, [loadDocuments]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter, typeFilter, ownerFilter]);

  const breadcrumbItems = useMemo(() => [
    { label: 'Compliance', href: '/compliance' },
    { label: 'Documents', icon: <FileText className="w-3.5 h-3.5" /> },
  ], []);

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Documents</h1>
          <p className="text-[var(--color-text-secondary)]">
            Track compliance documents, verification status, and expiry timelines.
          </p>
        </div>
        <Button onClick={() => navigate('/compliance/documents/new')} className="rounded-sm">
          <Plus className="w-4 h-4 mr-2" />
          Create Document
        </Button>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadDocuments} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <CardTitle className="text-base font-semibold">Document Inventory</CardTitle>
            <p className="text-sm text-[var(--color-text-secondary)]">
              {totalCount} document{totalCount !== 1 ? 's' : ''} in scope
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative w-64 max-w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
              <Input
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Search by reference, issuer, type..."
                className="pl-9"
              />
            </div>
            <Input
              value={typeFilter}
              onChange={(event) => setTypeFilter(event.target.value)}
              placeholder="Document type"
              className="h-9 w-40"
            />
            <Input
              value={ownerFilter}
              onChange={(event) => setOwnerFilter(event.target.value)}
              placeholder="Owner party ID"
              className="h-9 w-44"
            />
            <Select
              value={statusFilter || undefined}
              onValueChange={(value) => setStatusFilter(value === '__all__' ? '' : value)}
            >
              <SelectTrigger aria-label="Filter by status" className="h-9 w-40">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">All statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Pending">Pending</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Expired">Expired</SelectItem>
              </SelectContent>
            </Select>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={loadDocuments}
              title="Refresh"
              disabled={loading}
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50">
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Document
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Owner
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Status
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Issued
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Expires
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Files
                  </th>
                  <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Updated
                  </th>
                  <th className="text-right px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan={8} className="px-4 py-10 text-center text-[var(--color-text-tertiary)]">
                      <div className="flex items-center justify-center gap-2">
                        <div className="w-4 h-4 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
                        Loading documents...
                      </div>
                    </td>
                  </tr>
                ) : documents.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="px-4 py-10 text-center text-[var(--color-text-tertiary)]">
                      No documents match the current filters.
                    </td>
                  </tr>
                ) : (
                  documents.map((document) => (
                    <tr
                      key={document.documentId}
                      className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)]/30 cursor-pointer"
                      onClick={() => navigate(`/compliance/documents/${document.documentId}`)}
                    >
                      <td className="px-4 py-3">
                        <div className="font-medium text-[var(--color-text-primary)]">{document.documentType}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">{document.referenceNumber || '—'}</div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="text-[var(--color-text-primary)]">{document.ownerPartyId}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">{document.countryCode || '—'}</div>
                      </td>
                      <td className="px-4 py-3">
                        <Badge className={`rounded-full ${statusStyles[document.status] ?? 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]'}`}>
                          {document.status}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-[var(--color-text-secondary)]">
                        {formatDate(document.issuedOn)}
                      </td>
                      <td className="px-4 py-3 text-[var(--color-text-secondary)]">
                        <div className="flex items-center gap-2">
                          {formatDate(document.expiresOn)}
                          {isExpiringSoon(document.expiresOn) && (
                            <span className="text-xs text-[var(--color-warning)]">Expiring soon</span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-[var(--color-text-secondary)]">
                        {document.filesCount}
                      </td>
                      <td className="px-4 py-3 text-[var(--color-text-secondary)]">
                        {formatDate(document.updatedAt ?? document.createdAt)}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={(event) => {
                            event.stopPropagation();
                            navigate(`/compliance/documents/${document.documentId}`);
                          }}
                        >
                          View
                        </Button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          <div className="px-4 py-3 border-t border-[var(--color-border-light)]">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={handlePageSizeChange}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
