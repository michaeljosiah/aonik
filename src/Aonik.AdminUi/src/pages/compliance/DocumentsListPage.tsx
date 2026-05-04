import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { DataTablePagination } from '@/components/ui/data-table';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  AlertCircle,
  Calendar,
  FileText,
  Files,
  Plus,
  RefreshCw,
  Search,
} from 'lucide-react';
import { documentService } from '@/services/documentService';
import type { DocumentListItem, PagedResult } from '@/types';

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

const statusConfig: Record<string, { bg: string; text: string; dot: string }> = {
  Draft: {
    bg: 'bg-[var(--color-surface-inset)]',
    text: 'text-[var(--color-text-secondary)]',
    dot: 'bg-[var(--color-text-tertiary)]',
  },
  Pending: {
    bg: 'bg-[var(--color-warning-light)]',
    text: 'text-[var(--color-warning)]',
    dot: 'bg-[var(--color-warning)]',
  },
  Approved: {
    bg: 'bg-[var(--color-success-light)]',
    text: 'text-[var(--color-success)]',
    dot: 'bg-[var(--color-success)]',
  },
  Rejected: {
    bg: 'bg-[var(--color-error-light)]',
    text: 'text-[var(--color-error)]',
    dot: 'bg-[var(--color-error)]',
  },
  Expired: {
    bg: 'bg-[var(--color-pending-light)]',
    text: 'text-[var(--color-pending)]',
    dot: 'bg-[var(--color-pending)]',
  },
};

const fallbackStatus = {
  bg: 'bg-[var(--color-surface-inset)]',
  text: 'text-[var(--color-text-secondary)]',
  dot: 'bg-[var(--color-text-tertiary)]',
};

const formatDate = (dateString?: string | null) => {
  if (!dateString) return null;
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
};

const isExpiringSoon = (expiresOn?: string | null) => {
  if (!expiresOn) return false;
  const expiresAt = new Date(expiresOn).getTime();
  if (Number.isNaN(expiresAt)) return false;
  const daysRemaining = (expiresAt - Date.now()) / (1000 * 60 * 60 * 24);
  return daysRemaining >= 0 && daysRemaining <= 30;
};

/* -------------------------------------------------------------------------- */
/*  Document Card                                                              */
/* -------------------------------------------------------------------------- */

function DocumentCard({
  document,
  onClick,
}: {
  document: DocumentListItem;
  onClick: () => void;
}) {
  const status = statusConfig[document.status] ?? fallbackStatus;
  const expiring = isExpiringSoon(document.expiresOn);
  const expiryDate = formatDate(document.expiresOn);

  return (
    <Card
      className="group cursor-pointer transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md"
      onClick={onClick}
    >
      <CardContent className="p-5">
        {/* Header row */}
        <div className="mb-3 flex items-start justify-between">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[var(--color-brand-primary-light)]">
            <FileText className="h-5 w-5 text-[var(--color-brand-primary)]" />
          </div>
          <Badge className={`rounded-full text-xs ${status.bg} ${status.text}`}>
            <span className={`mr-1.5 inline-block h-1.5 w-1.5 rounded-full ${status.dot}`} />
            {document.status}
          </Badge>
        </div>

        {/* Title */}
        <h3 className="mb-1 text-base font-semibold text-[var(--color-text-primary)] group-hover:text-[var(--color-brand-primary)] transition-colors">
          {document.documentType}
        </h3>
        {document.referenceNumber && (
          <p className="mb-3 text-xs text-[var(--color-text-tertiary)] font-mono">
            {document.referenceNumber}
          </p>
        )}

        {/* Meta row */}
        <div className="flex items-center gap-4 text-xs text-[var(--color-text-tertiary)]">
          <span className="flex items-center gap-1">
            <Files className="h-3.5 w-3.5" />
            {document.filesCount} file{document.filesCount !== 1 ? 's' : ''}
          </span>
          {expiryDate && (
            <span className={`flex items-center gap-1 ${expiring ? 'text-[var(--color-warning)] font-medium' : ''}`}>
              <Calendar className="h-3.5 w-3.5" />
              {expiring ? 'Expires ' : ''}{expiryDate}
            </span>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/*  Main Page                                                                  */
/* -------------------------------------------------------------------------- */

export function DocumentsListPage() {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(12);
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
      });
      setDocuments(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      console.error('Failed to load documents:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load documents. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, searchQuery, statusFilter]);

  useEffect(() => {
    loadDocuments();
  }, [loadDocuments]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter]);

  return (
    <div className="h-full overflow-auto p-6">

      {/* Header */}
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Documents</h1>
          <p className="text-[var(--color-text-secondary)]">
            Upload, organise, and manage compliance documents.
          </p>
        </div>
        <Button onClick={() => navigate('/compliance/documents/new')}>
          <Plus className="mr-2 h-4 w-4" />
          New Document
        </Button>
      </div>

      {/* Filters */}
      <div className="mb-6 flex flex-wrap items-center gap-3">
        <div className="relative w-72 max-w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--color-text-tertiary)]" />
          <Input
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search documents..."
            className="pl-9"
          />
        </div>
        <Select
          value={statusFilter || undefined}
          onValueChange={(value) => setStatusFilter(value === '__all__' ? '' : value)}
        >
          <SelectTrigger aria-label="Filter by status" className="h-9 w-36">
            <SelectValue placeholder="All statuses" />
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
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
        </Button>
        <span className="ml-auto text-sm text-[var(--color-text-tertiary)]">
          {totalCount} document{totalCount !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Error */}
      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
            <AlertCircle className="h-5 w-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadDocuments} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Document grid */}
      {loading ? (
        <div className="flex items-center justify-center py-20">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-[var(--color-brand-primary)] border-t-transparent" />
        </div>
      ) : documents.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--color-surface-inset)]">
            <FileText className="h-8 w-8 text-[var(--color-text-tertiary)]" />
          </div>
          <p className="mb-1 text-base font-medium text-[var(--color-text-secondary)]">No documents found</p>
          <p className="mb-4 text-sm text-[var(--color-text-tertiary)]">
            {searchQuery || statusFilter
              ? 'Try adjusting your search or filters.'
              : 'Get started by uploading your first document.'}
          </p>
          {!searchQuery && !statusFilter && (
            <Button onClick={() => navigate('/compliance/documents/new')}>
              <Plus className="mr-2 h-4 w-4" />
              New Document
            </Button>
          )}
        </div>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {documents.map((doc) => (
              <DocumentCard
                key={doc.documentId}
                document={doc}
                onClick={() => navigate(`/compliance/documents/${doc.documentId}`)}
              />
            ))}
          </div>
          <div className="mt-6">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPageNumber(1);
              }}
            />
          </div>
        </>
      )}
    </div>
  );
}
