import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  AlertTriangle,
  ArrowLeft,
  Calendar,
  CalendarClock,
  CheckCircle2,
  CloudUpload,
  FileText,
  Files,
  Hash,
  Pencil,
  ShieldCheck,
  X,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { documentService } from '@/services/documentService';
import type {
  DocumentDetailsResponse,
  DocumentUsageResponse,
} from '@/types';

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '\u2014';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const formatDateTime = (dateString?: string | null) => {
  if (!dateString) return '\u2014';
  return new Date(dateString).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

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

function formatFileSize(bytes?: number | null): string {
  if (!bytes) return '\u2014';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const flattenVerifications = (usages: DocumentUsageResponse[]) =>
  usages.flatMap((usage) =>
    usage.verifications.map((v) => ({ usage, verification: v })),
  );

const isExpiringSoon = (expiresOn?: string | null) => {
  if (!expiresOn) return false;
  const expiresAt = new Date(expiresOn).getTime();
  if (Number.isNaN(expiresAt)) return false;
  const daysRemaining = (expiresAt - Date.now()) / (1000 * 60 * 60 * 24);
  return daysRemaining >= 0 && daysRemaining <= 30;
};

/* -------------------------------------------------------------------------- */
/*  Component                                                                  */
/* -------------------------------------------------------------------------- */

export function DocumentDetailPage() {
  const navigate = useNavigate();
  const { documentId } = useParams<{ documentId: string }>();

  // Data
  const [doc, setDoc] = useState<DocumentDetailsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // File upload
  const [pendingFiles, setPendingFiles] = useState<File[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

  // Sections
  const [usageOpen, setUsageOpen] = useState(false);
  const [verificationsOpen, setVerificationsOpen] = useState(false);
  const [versionsOpen, setVersionsOpen] = useState(false);

  /* ---------------------------------------------------------------------- */
  /*  Load                                                                    */
  /* ---------------------------------------------------------------------- */

  const loadDocument = useCallback(async () => {
    if (!documentId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await documentService.get(documentId);
      setDoc(data);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load document.');
    } finally {
      setLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    loadDocument();
  }, [loadDocument]);

  /* ---------------------------------------------------------------------- */
  /*  File handling                                                          */
  /* ---------------------------------------------------------------------- */

  const addFiles = useCallback((incoming: FileList | File[]) => {
    const newFiles = Array.from(incoming);
    setPendingFiles((prev) => {
      const existing = new Set(prev.map((f) => `${f.name}:${f.size}`));
      return [...prev, ...newFiles.filter((f) => !existing.has(`${f.name}:${f.size}`))];
    });
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      if (e.dataTransfer.files.length > 0) addFiles(e.dataTransfer.files);
    },
    [addFiles],
  );

  const handleUpload = async () => {
    if (!documentId || pendingFiles.length === 0) return;
    setIsUploading(true);
    try {
      for (const file of pendingFiles) {
        await documentService.uploadFile(documentId, { file });
      }
      toast.success(`${pendingFiles.length} file${pendingFiles.length > 1 ? 's' : ''} uploaded.`);
      setPendingFiles([]);
      await loadDocument();
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to upload files.');
    } finally {
      setIsUploading(false);
    }
  };

  /* ---------------------------------------------------------------------- */
  /*  Render states                                                          */
  /* ---------------------------------------------------------------------- */

  const breadcrumbItems = useMemo(
    () => [
      { label: 'Compliance', href: '/compliance' },
      { label: 'Documents', href: '/compliance/documents' },
      {
        label: doc?.document.documentType ?? 'Document',
        icon: <FileText className="w-3.5 h-3.5" />,
      },
    ],
    [doc?.document.documentType],
  );

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-[var(--color-brand-primary)] border-t-transparent" />
      </div>
    );
  }

  if (error || !doc) {
    return (
      <div className="h-full overflow-auto p-6">
        <Breadcrumb
          items={[
            { label: 'Compliance', href: '/compliance' },
            { label: 'Documents', href: '/compliance/documents' },
          ]}
          className="mb-4"
        />
        <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
            <AlertTriangle className="h-5 w-5" />
            <span>{error || 'Document not found.'}</span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => navigate('/compliance/documents')}
              className="ml-auto"
            >
              Back to documents
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const status = statusConfig[doc.document.status] ?? fallbackStatus;
  const verifications = flattenVerifications(doc.usages);
  const expiring = isExpiringSoon(doc.document.expiresOn);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      {/* Header */}
      <div className="mb-8 flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon-sm" onClick={() => navigate('/compliance/documents')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
                {doc.document.documentType}
              </h1>
              <Badge className={`rounded-full text-xs ${status.bg} ${status.text}`}>
                <span className={`mr-1.5 inline-block h-1.5 w-1.5 rounded-full ${status.dot}`} />
                {doc.document.status}
              </Badge>
            </div>
            <p className="mt-0.5 text-sm text-[var(--color-text-tertiary)]">
              Owner: {doc.document.ownerPartyId}
            </p>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-[64rem] space-y-6">
        {/* ================================================================ */}
        {/*  Files section — primary                                          */}
        {/* ================================================================ */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <div className="flex items-center gap-2">
              <Files className="h-4 w-4 text-[var(--color-text-tertiary)]" />
              <CardTitle className="text-sm">
                Files ({doc.files.length})
              </CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Existing files */}
            {doc.files.length > 0 && (
              <div className="space-y-2">
                {doc.files.map((file) => (
                  <div
                    key={file.documentFileId}
                    className="flex items-center gap-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/40 px-4 py-3"
                  >
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-[var(--color-brand-primary-light)]">
                      <FileText className="h-4 w-4 text-[var(--color-brand-primary)]" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-[var(--color-text-primary)]">
                        {file.fileName || file.storageKey}
                      </p>
                      <p className="text-xs text-[var(--color-text-tertiary)]">
                        {file.contentType} \u00b7 {formatFileSize(file.fileSizeBytes)}
                      </p>
                    </div>
                    <div className="text-right text-xs text-[var(--color-text-tertiary)]">
                      <p>{file.storageProvider}</p>
                      <p>{formatDateTime(file.createdAt)}</p>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {doc.files.length === 0 && pendingFiles.length === 0 && (
              <p className="text-sm text-[var(--color-text-tertiary)]">No files attached yet.</p>
            )}

            {/* Upload drop zone */}
            <div
              className={`flex flex-col items-center justify-center rounded-xl border-2 border-dashed px-6 py-8 transition-colors ${
                isDragOver
                  ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                  : 'border-[var(--color-border-light)] hover:border-[var(--color-brand-primary)]/50'
              }`}
              onDragOver={(e) => {
                e.preventDefault();
                setIsDragOver(true);
              }}
              onDragLeave={() => setIsDragOver(false)}
              onDrop={handleDrop}
            >
              <CloudUpload className="mb-2 h-6 w-6 text-[var(--color-text-tertiary)]" />
              <p className="mb-1 text-sm text-[var(--color-text-secondary)]">
                Drag & drop files to upload
              </p>
              <p className="mb-3 text-xs text-[var(--color-text-tertiary)]">or click to browse</p>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  const input = document.createElement('input');
                  input.type = 'file';
                  input.multiple = true;
                  input.onchange = () => {
                    if (input.files) addFiles(input.files);
                  };
                  input.click();
                }}
              >
                Choose Files
              </Button>
            </div>

            {/* Pending uploads */}
            {pendingFiles.length > 0 && (
              <div className="space-y-2">
                <p className="text-xs font-medium text-[var(--color-text-secondary)]">
                  Ready to upload ({pendingFiles.length})
                </p>
                {pendingFiles.map((file, idx) => (
                  <div
                    key={`${file.name}-${file.size}`}
                    className="flex items-center gap-3 rounded-lg border border-[var(--color-brand-primary)]/30 bg-[var(--color-brand-primary-light)]/40 px-4 py-3"
                  >
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-[var(--color-brand-primary-light)]">
                      <FileText className="h-4 w-4 text-[var(--color-brand-primary)]" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-[var(--color-text-primary)]">
                        {file.name}
                      </p>
                      <p className="text-xs text-[var(--color-text-tertiary)]">
                        {formatFileSize(file.size)}
                        {file.type && ` \u00b7 ${file.type}`}
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => setPendingFiles((p) => p.filter((_, i) => i !== idx))}
                      className="rounded-md p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-error)] transition-colors"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </div>
                ))}
                <Button onClick={handleUpload} disabled={isUploading} className="mt-2">
                  <CloudUpload className="mr-2 h-4 w-4" />
                  {isUploading ? 'Uploading...' : `Upload ${pendingFiles.length} File${pendingFiles.length > 1 ? 's' : ''}`}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>

        {/* ================================================================ */}
        {/*  Document details / metadata                                      */}
        {/* ================================================================ */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <div className="flex items-center gap-2">
              <Pencil className="h-4 w-4 text-[var(--color-text-tertiary)]" />
              <CardTitle className="text-sm">Document Details</CardTitle>
            </div>
          </CardHeader>
          <CardContent>
            <div className="grid gap-x-8 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
              <DetailField label="Issuer" value={doc.document.issuerName} />
              <DetailField label="Country" value={doc.document.countryCode} />
              <DetailField label="Reference" value={doc.document.referenceNumber} mono />
              <DetailField label="Issued On" value={formatDate(doc.document.issuedOn)} />
              <DetailField
                label="Expires On"
                value={formatDate(doc.document.expiresOn)}
                highlight={expiring}
                highlightLabel="Expiring soon"
              />
              <DetailField
                label="Created"
                value={formatDateTime(doc.document.createdAt)}
              />
              <DetailField
                label="Last Updated"
                value={formatDateTime(doc.document.updatedAt)}
              />
              {doc.document.tags.length > 0 && (
                <div className="sm:col-span-2 lg:col-span-3">
                  <p className="mb-1 text-xs text-[var(--color-text-tertiary)]">Tags</p>
                  <div className="flex flex-wrap gap-1.5">
                    {doc.document.tags.map((tag) => (
                      <Badge key={tag} variant="secondary" className="rounded-full text-xs">
                        {tag}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </CardContent>
        </Card>

        {/* ================================================================ */}
        {/*  Collapsible advanced sections                                    */}
        {/* ================================================================ */}

        {/* Usage Records */}
        <Collapsible open={usageOpen} onOpenChange={setUsageOpen}>
          <Card>
            <CollapsibleTrigger asChild>
              <CardHeader className="cursor-pointer select-none hover:bg-[var(--color-surface-inset)]/30 transition-colors">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ShieldCheck className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                    <CardTitle className="text-sm">
                      Usage Records ({doc.usages.length})
                    </CardTitle>
                  </div>
                  <span className="text-xs text-[var(--color-text-tertiary)]">
                    {usageOpen ? 'Collapse' : 'Expand'}
                  </span>
                </div>
              </CardHeader>
            </CollapsibleTrigger>
            <CollapsibleContent>
              <CardContent className="space-y-3 pt-0">
                {doc.usages.length === 0 ? (
                  <p className="text-sm text-[var(--color-text-tertiary)]">No usage records.</p>
                ) : (
                  doc.usages.map((usage) => (
                    <div
                      key={usage.documentUsageId}
                      className="flex items-start justify-between gap-4 rounded-lg border border-[var(--color-border-light)] px-4 py-3"
                    >
                      <div>
                        <p className="text-sm font-medium text-[var(--color-text-primary)]">
                          {usage.purpose}
                        </p>
                        <p className="text-xs text-[var(--color-text-tertiary)]">
                          {usage.relatedEntityType
                            ? `${usage.relatedEntityType} \u00b7 ${usage.relatedEntityId ?? '\u2014'}`
                            : 'No related entity'}
                        </p>
                        <p className="text-xs text-[var(--color-text-tertiary)]">
                          Status: {usage.status} \u00b7 Verified: {formatDateTime(usage.verifiedAt)}
                        </p>
                      </div>
                      <Badge variant="outline" className="text-xs shrink-0">
                        {usage.verifications.length} verification{usage.verifications.length !== 1 ? 's' : ''}
                      </Badge>
                    </div>
                  ))
                )}
              </CardContent>
            </CollapsibleContent>
          </Card>
        </Collapsible>

        {/* Verifications */}
        <Collapsible open={verificationsOpen} onOpenChange={setVerificationsOpen}>
          <Card>
            <CollapsibleTrigger asChild>
              <CardHeader className="cursor-pointer select-none hover:bg-[var(--color-surface-inset)]/30 transition-colors">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <CheckCircle2 className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                    <CardTitle className="text-sm">
                      Verifications ({verifications.length})
                    </CardTitle>
                  </div>
                  <span className="text-xs text-[var(--color-text-tertiary)]">
                    {verificationsOpen ? 'Collapse' : 'Expand'}
                  </span>
                </div>
              </CardHeader>
            </CollapsibleTrigger>
            <CollapsibleContent>
              <CardContent className="space-y-3 pt-0">
                {verifications.length === 0 ? (
                  <p className="text-sm text-[var(--color-text-tertiary)]">No verifications recorded.</p>
                ) : (
                  verifications.map(({ usage, verification }) => (
                    <div
                      key={verification.documentVerificationId}
                      className="flex items-start gap-3 rounded-lg border border-[var(--color-border-light)] px-4 py-3"
                    >
                      <CalendarClock className="mt-0.5 h-4 w-4 shrink-0 text-[var(--color-text-tertiary)]" />
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-medium text-[var(--color-text-primary)]">
                            {verification.decision}
                          </span>
                          <Badge variant="outline" className="text-xs">
                            {usage.purpose}
                          </Badge>
                        </div>
                        <p className="text-xs text-[var(--color-text-tertiary)]">
                          {verification.verifierType} \u00b7{' '}
                          {verification.verifierId || 'Unknown'} \u00b7{' '}
                          {formatDateTime(verification.createdAt)}
                        </p>
                        {verification.decisionNotes && (
                          <p className="mt-1 text-xs text-[var(--color-text-secondary)]">
                            {verification.decisionNotes}
                          </p>
                        )}
                        {verification.aiRunId && (
                          <p className="mt-1 flex items-center gap-1.5 text-xs text-[var(--color-text-tertiary)]">
                            <Hash className="h-3 w-3" />
                            AI Run: {verification.aiRunId}
                          </p>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </CardContent>
            </CollapsibleContent>
          </Card>
        </Collapsible>

        {/* Versions */}
        <Collapsible open={versionsOpen} onOpenChange={setVersionsOpen}>
          <Card>
            <CollapsibleTrigger asChild>
              <CardHeader className="cursor-pointer select-none hover:bg-[var(--color-surface-inset)]/30 transition-colors">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Calendar className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                    <CardTitle className="text-sm">
                      Versions ({doc.versions.length})
                    </CardTitle>
                  </div>
                  <span className="text-xs text-[var(--color-text-tertiary)]">
                    {versionsOpen ? 'Collapse' : 'Expand'}
                  </span>
                </div>
              </CardHeader>
            </CollapsibleTrigger>
            <CollapsibleContent>
              <CardContent className="space-y-3 pt-0">
                {doc.versions.length === 0 ? (
                  <p className="text-sm text-[var(--color-text-tertiary)]">No versions recorded.</p>
                ) : (
                  doc.versions.map((version) => (
                    <div
                      key={version.documentVersionId}
                      className="flex items-start justify-between gap-4 rounded-lg border border-[var(--color-border-light)] px-4 py-3"
                    >
                      <div>
                        <p className="text-sm font-medium text-[var(--color-text-primary)]">
                          Version {version.version}
                        </p>
                        <p className="text-xs text-[var(--color-text-tertiary)]">
                          Status: {version.status} \u00b7 Submitted: {formatDateTime(version.submittedAt)}
                        </p>
                      </div>
                      <p className="text-xs text-[var(--color-text-tertiary)]">
                        Decisioned: {formatDateTime(version.decisionedAt)}
                      </p>
                    </div>
                  ))
                )}
              </CardContent>
            </CollapsibleContent>
          </Card>
        </Collapsible>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/*  Detail Field                                                               */
/* -------------------------------------------------------------------------- */

function DetailField({
  label,
  value,
  mono,
  highlight,
  highlightLabel,
}: {
  label: string;
  value?: string | null;
  mono?: boolean;
  highlight?: boolean;
  highlightLabel?: string;
}) {
  return (
    <div>
      <p className="mb-0.5 text-xs text-[var(--color-text-tertiary)]">{label}</p>
      <div className="flex items-center gap-2">
        <p
          className={`text-sm text-[var(--color-text-primary)] ${mono ? 'font-mono' : ''} ${
            highlight ? 'text-[var(--color-warning)] font-medium' : ''
          }`}
        >
          {value || '\u2014'}
        </p>
        {highlight && highlightLabel && (
          <Badge className="rounded-full bg-[var(--color-warning-light)] text-[var(--color-warning)] text-xs">
            {highlightLabel}
          </Badge>
        )}
      </div>
    </div>
  );
}
