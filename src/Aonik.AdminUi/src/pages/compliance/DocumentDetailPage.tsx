import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  FileText,
  FileUp,
  ClipboardCheck,
  CheckCircle2,
  AlertTriangle,
  ArrowLeft,
  CalendarClock,
  Hash,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { documentService } from '@/services/documentService';
import type {
  AddDocumentFileRequest,
  AddDocumentUsageRequest,
  AddDocumentVerificationRequest,
  DocumentDetailsResponse,
  DocumentUsageResponse,
} from '@/types';

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const formatDateTime = (dateString?: string | null) => {
  if (!dateString) return '—';
  return new Date(dateString).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const statusStyles: Record<string, string> = {
  Draft: 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]',
  Pending: 'bg-[var(--color-warning-light)] text-[var(--color-warning)]',
  Approved: 'bg-[var(--color-success-light)] text-[var(--color-success)]',
  Rejected: 'bg-[var(--color-error-light)] text-[var(--color-error)]',
  Expired: 'bg-[var(--color-pending-light)] text-[var(--color-pending)]',
};

const formatAttributes = (attributesJson: string) => {
  try {
    return JSON.stringify(JSON.parse(attributesJson), null, 2);
  } catch {
    return attributesJson;
  }
};

const flattenVerifications = (usages: DocumentUsageResponse[]) => {
  return usages.flatMap((usage) =>
    usage.verifications.map((verification) => ({
      usage,
      verification,
    }))
  );
};

export function DocumentDetailPage() {
  const navigate = useNavigate();
  const { documentId } = useParams<{ documentId: string }>();
  const [document, setDocument] = useState<DocumentDetailsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [fileForm, setFileForm] = useState<AddDocumentFileRequest>({
    storageProvider: '',
    storageContainer: '',
    storageKey: '',
    contentType: '',
    fileName: '',
    fileSizeBytes: undefined,
    sha256: '',
    pageIndex: undefined,
    side: '',
    capturedAt: '',
    capturedBy: '',
    metadataJson: '',
  });
  const [usageForm, setUsageForm] = useState<AddDocumentUsageRequest>({
    ownerPartyId: '',
    purpose: '',
    relatedEntityType: '',
    relatedEntityId: '',
    status: '',
    notes: '',
  });
  const [verificationForm, setVerificationForm] = useState<AddDocumentVerificationRequest>({
    decision: '',
    decisionReasonCode: '',
    decisionNotes: '',
    verifierType: '',
    verifierId: '',
    aiRunId: '',
  });
  const [selectedUsageId, setSelectedUsageId] = useState('');
  const [isSubmittingFile, setIsSubmittingFile] = useState(false);
  const [isSubmittingUsage, setIsSubmittingUsage] = useState(false);
  const [isSubmittingVerification, setIsSubmittingVerification] = useState(false);

  const loadDocument = useCallback(async () => {
    if (!documentId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await documentService.get(documentId);
      setDocument(data);
      setUsageForm((prev) => ({ ...prev, ownerPartyId: data.document.ownerPartyId }));
      setSelectedUsageId((prev) => prev || data.usages[0]?.documentUsageId || '');
    } catch (err: unknown) {
      console.error('Failed to load document:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load document. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    loadDocument();
  }, [loadDocument]);

  const breadcrumbItems = useMemo(() => [
    { label: 'Compliance', href: '/compliance' },
    { label: 'Documents', href: '/compliance/documents' },
    { label: document?.document.documentType ?? 'Document', icon: <FileText className="w-3.5 h-3.5" /> },
  ], [document?.document.documentType]);

  const handleAddFile = async () => {
    if (!documentId) return;
    if (!fileForm.storageProvider || !fileForm.storageKey || !fileForm.contentType) {
      toast.error('Storage provider, key, and content type are required.');
      return;
    }
    setIsSubmittingFile(true);
    try {
      await documentService.addFile(documentId, {
        storageProvider: fileForm.storageProvider,
        storageContainer: fileForm.storageContainer || undefined,
        storageKey: fileForm.storageKey,
        contentType: fileForm.contentType,
        fileName: fileForm.fileName || undefined,
        fileSizeBytes: fileForm.fileSizeBytes ? Number(fileForm.fileSizeBytes) : undefined,
        sha256: fileForm.sha256 || undefined,
        pageIndex: fileForm.pageIndex ? Number(fileForm.pageIndex) : undefined,
        side: fileForm.side || undefined,
        capturedAt: fileForm.capturedAt || undefined,
        capturedBy: fileForm.capturedBy || undefined,
        metadataJson: fileForm.metadataJson || '{}',
      });
      toast.success('Document file added.');
      setFileForm({
        storageProvider: '',
        storageContainer: '',
        storageKey: '',
        contentType: '',
        fileName: '',
        fileSizeBytes: undefined,
        sha256: '',
        pageIndex: undefined,
        side: '',
        capturedAt: '',
        capturedBy: '',
        metadataJson: '',
      });
      await loadDocument();
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      toast.error(message || 'Failed to add document file.');
    } finally {
      setIsSubmittingFile(false);
    }
  };

  const handleAddUsage = async () => {
    if (!documentId) return;
    if (!usageForm.purpose) {
      toast.error('Usage purpose is required.');
      return;
    }
    setIsSubmittingUsage(true);
    try {
      const result = await documentService.addUsage(documentId, {
        ...usageForm,
        ownerPartyId: usageForm.ownerPartyId || document?.document.ownerPartyId || '',
        relatedEntityId: usageForm.relatedEntityId || undefined,
        status: usageForm.status || undefined,
      });
      toast.success('Usage added to document.');
      setUsageForm((prev) => ({
        ownerPartyId: prev.ownerPartyId,
        purpose: '',
        relatedEntityType: '',
        relatedEntityId: '',
        status: '',
        notes: '',
      }));
      setSelectedUsageId(result.documentUsageId);
      await loadDocument();
      setActiveTab('usage');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      toast.error(message || 'Failed to add document usage.');
    } finally {
      setIsSubmittingUsage(false);
    }
  };

  const handleAddVerification = async () => {
    if (!selectedUsageId) {
      toast.error('Select a usage record to verify.');
      return;
    }
    if (!verificationForm.decision || !verificationForm.verifierType) {
      toast.error('Decision and verifier type are required.');
      return;
    }
    setIsSubmittingVerification(true);
    try {
      await documentService.addVerification(selectedUsageId, {
        ...verificationForm,
        decisionReasonCode: verificationForm.decisionReasonCode || undefined,
        decisionNotes: verificationForm.decisionNotes || undefined,
        verifierId: verificationForm.verifierId || undefined,
        aiRunId: verificationForm.aiRunId || undefined,
      });
      toast.success('Verification recorded.');
      setVerificationForm({
        decision: '',
        decisionReasonCode: '',
        decisionNotes: '',
        verifierType: '',
        verifierId: '',
        aiRunId: '',
      });
      await loadDocument();
      setActiveTab('verification');
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      toast.error(message || 'Failed to add verification.');
    } finally {
      setIsSubmittingVerification(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="w-8 h-8 border-4 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (error || !document) {
    return (
      <div className="h-full overflow-auto p-6">
        <Breadcrumb items={[{ label: 'Compliance', href: '/compliance' }, { label: 'Documents', href: '/compliance/documents' }]} className="mb-4" />
        <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertTriangle className="w-5 h-5" />
            <span>{error || 'Document not found.'}</span>
            <Button variant="outline" size="sm" onClick={() => navigate('/compliance/documents')} className="ml-auto">
              Back to documents
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const verifications = flattenVerifications(document.usages);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex flex-wrap items-center justify-between gap-4 mb-6">
        <div>
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="icon-sm" onClick={() => navigate('/compliance/documents')}>
              <ArrowLeft className="w-4 h-4" />
            </Button>
            <div>
              <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{document.document.documentType}</h1>
              <p className="text-[var(--color-text-secondary)]">Owner: {document.document.ownerPartyId}</p>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <Badge className={`rounded-full ${statusStyles[document.document.status] ?? 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]'}`}>
            {document.document.status}
          </Badge>
          <Button variant="outline" onClick={() => setActiveTab('files')}>
            <FileUp className="w-4 h-4 mr-2" />
            Add File
          </Button>
          <Button variant="outline" onClick={() => setActiveTab('usage')}>
            <ClipboardCheck className="w-4 h-4 mr-2" />
            Add Usage
          </Button>
          <Button onClick={() => setActiveTab('verification')}>
            <CheckCircle2 className="w-4 h-4 mr-2" />
            Add Verification
          </Button>
        </div>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="mb-4">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="files">Files</TabsTrigger>
          <TabsTrigger value="usage">Usage</TabsTrigger>
          <TabsTrigger value="verification">Verification</TabsTrigger>
          <TabsTrigger value="versions">Versions</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <div className="grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle className="text-sm">Document Summary</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 sm:grid-cols-2 text-sm">
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Issuer</div>
                  <div className="text-[var(--color-text-primary)]">{document.document.issuerName || '—'}</div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Country</div>
                  <div className="text-[var(--color-text-primary)]">{document.document.countryCode || '—'}</div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Reference</div>
                  <div className="text-[var(--color-text-primary)]">{document.document.referenceNumber || '—'}</div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Issued On</div>
                  <div className="text-[var(--color-text-primary)]">{formatDate(document.document.issuedOn)}</div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Expires On</div>
                  <div className="text-[var(--color-text-primary)]">{formatDate(document.document.expiresOn)}</div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Tags</div>
                  <div className="flex flex-wrap gap-2">
                    {document.document.tags.length === 0 ? (
                      <span className="text-[var(--color-text-tertiary)]">—</span>
                    ) : (
                      document.document.tags.map((tag) => (
                        <Badge key={tag} variant="secondary" className="rounded-full text-xs">
                          {tag}
                        </Badge>
                      ))
                    )}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Created</div>
                  <div className="text-[var(--color-text-primary)]">{formatDateTime(document.document.createdAt)}</div>
                </div>
                <div>
                  <div className="text-xs text-[var(--color-text-tertiary)]">Updated</div>
                  <div className="text-[var(--color-text-primary)]">{formatDateTime(document.document.updatedAt)}</div>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Attributes</CardTitle>
              </CardHeader>
              <CardContent>
                <pre className="text-xs whitespace-pre-wrap bg-[var(--color-surface-inset)]/60 border border-[var(--color-border-light)] rounded-md p-3">
                  {formatAttributes(document.document.attributesJson)}
                </pre>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="files">
          <div className="grid gap-6">
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Add a File</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 md:grid-cols-2 text-sm">
                <Input
                  value={fileForm.storageProvider}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, storageProvider: event.target.value }))}
                  placeholder="Storage provider (e.g., S3, Azure)"
                />
                <Input
                  value={fileForm.storageKey}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, storageKey: event.target.value }))}
                  placeholder="Storage key"
                />
                <Input
                  value={fileForm.contentType}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, contentType: event.target.value }))}
                  placeholder="Content type"
                />
                <Input
                  value={fileForm.fileName ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, fileName: event.target.value }))}
                  placeholder="File name"
                />
                <Input
                  value={fileForm.storageContainer ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, storageContainer: event.target.value }))}
                  placeholder="Storage container"
                />
                <Input
                  value={fileForm.fileSizeBytes?.toString() ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, fileSizeBytes: event.target.value ? Number(event.target.value) : undefined }))}
                  placeholder="File size (bytes)"
                  type="number"
                />
                <Input
                  value={fileForm.sha256 ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, sha256: event.target.value }))}
                  placeholder="SHA-256 hash"
                />
                <Input
                  value={fileForm.pageIndex?.toString() ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, pageIndex: event.target.value ? Number(event.target.value) : undefined }))}
                  placeholder="Page index"
                  type="number"
                />
                <Input
                  value={fileForm.side ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, side: event.target.value }))}
                  placeholder="Side (front/back)"
                />
                <Input
                  value={fileForm.capturedAt ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, capturedAt: event.target.value }))}
                  placeholder="Captured at (ISO)"
                />
                <Input
                  value={fileForm.capturedBy ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, capturedBy: event.target.value }))}
                  placeholder="Captured by"
                />
                <Textarea
                  value={fileForm.metadataJson ?? ''}
                  onChange={(event) => setFileForm((prev) => ({ ...prev, metadataJson: event.target.value }))}
                  placeholder="Metadata JSON"
                  className="md:col-span-2"
                />
                <div className="md:col-span-2">
                  <Button onClick={handleAddFile} disabled={isSubmittingFile}>
                    {isSubmittingFile ? 'Saving...' : 'Add File'}
                  </Button>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Files</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {document.files.length === 0 ? (
                  <div className="text-sm text-[var(--color-text-tertiary)]">No files attached.</div>
                ) : (
                  document.files.map((file) => (
                    <div key={file.documentFileId} className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-4 last:border-b-0">
                      <div>
                        <div className="text-sm font-medium text-[var(--color-text-primary)]">{file.fileName || file.storageKey}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">{file.contentType} · {file.fileSizeBytes ?? '—'} bytes</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">SHA-256: {file.sha256 || '—'}</div>
                      </div>
                      <div className="text-xs text-[var(--color-text-tertiary)] text-right">
                        <div>{file.storageProvider}</div>
                        <div>{formatDateTime(file.createdAt)}</div>
                      </div>
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="usage">
          <div className="grid gap-6">
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Add Usage</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 md:grid-cols-2 text-sm">
                <Input
                  value={usageForm.ownerPartyId}
                  onChange={(event) => setUsageForm((prev) => ({ ...prev, ownerPartyId: event.target.value }))}
                  placeholder="Owner party ID"
                />
                <Input
                  value={usageForm.purpose}
                  onChange={(event) => setUsageForm((prev) => ({ ...prev, purpose: event.target.value }))}
                  placeholder="Purpose (e.g., KYC)"
                />
                <Input
                  value={usageForm.relatedEntityType ?? ''}
                  onChange={(event) => setUsageForm((prev) => ({ ...prev, relatedEntityType: event.target.value }))}
                  placeholder="Related entity type"
                />
                <Input
                  value={usageForm.relatedEntityId ?? ''}
                  onChange={(event) => setUsageForm((prev) => ({ ...prev, relatedEntityId: event.target.value }))}
                  placeholder="Related entity ID"
                />
                <Input
                  value={usageForm.status ?? ''}
                  onChange={(event) => setUsageForm((prev) => ({ ...prev, status: event.target.value }))}
                  placeholder="Status (Pending, Satisfied)"
                />
                <Textarea
                  value={usageForm.notes ?? ''}
                  onChange={(event) => setUsageForm((prev) => ({ ...prev, notes: event.target.value }))}
                  placeholder="Notes"
                  className="md:col-span-2"
                />
                <div className="md:col-span-2">
                  <Button onClick={handleAddUsage} disabled={isSubmittingUsage}>
                    {isSubmittingUsage ? 'Saving...' : 'Add Usage'}
                  </Button>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Usage Records</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {document.usages.length === 0 ? (
                  <div className="text-sm text-[var(--color-text-tertiary)]">No usage records.</div>
                ) : (
                  document.usages.map((usage) => (
                    <div key={usage.documentUsageId} className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-4 last:border-b-0">
                      <div>
                        <div className="text-sm font-medium text-[var(--color-text-primary)]">{usage.purpose}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">
                          {usage.relatedEntityType ? `${usage.relatedEntityType} · ${usage.relatedEntityId ?? '—'}` : 'No related entity'}
                        </div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">Status: {usage.status}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">Verified: {formatDateTime(usage.verifiedAt)}</div>
                      </div>
                      <Badge variant="outline" className="text-xs">
                        {usage.verifications.length} verifications
                      </Badge>
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="verification">
          <div className="grid gap-6">
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Add Verification</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 md:grid-cols-2 text-sm">
                <Input
                  value={selectedUsageId}
                  onChange={(event) => setSelectedUsageId(event.target.value)}
                  placeholder="Document usage ID"
                />
                <Input
                  value={verificationForm.decision}
                  onChange={(event) => setVerificationForm((prev) => ({ ...prev, decision: event.target.value }))}
                  placeholder="Decision (Approved/Rejected)"
                />
                <Input
                  value={verificationForm.verifierType}
                  onChange={(event) => setVerificationForm((prev) => ({ ...prev, verifierType: event.target.value }))}
                  placeholder="Verifier type (Human/AI)"
                />
                <Input
                  value={verificationForm.verifierId ?? ''}
                  onChange={(event) => setVerificationForm((prev) => ({ ...prev, verifierId: event.target.value }))}
                  placeholder="Verifier ID"
                />
                <Input
                  value={verificationForm.decisionReasonCode ?? ''}
                  onChange={(event) => setVerificationForm((prev) => ({ ...prev, decisionReasonCode: event.target.value }))}
                  placeholder="Decision reason code"
                />
                <Input
                  value={verificationForm.aiRunId ?? ''}
                  onChange={(event) => setVerificationForm((prev) => ({ ...prev, aiRunId: event.target.value }))}
                  placeholder="AI Run ID"
                />
                <Textarea
                  value={verificationForm.decisionNotes ?? ''}
                  onChange={(event) => setVerificationForm((prev) => ({ ...prev, decisionNotes: event.target.value }))}
                  placeholder="Decision notes"
                  className="md:col-span-2"
                />
                <div className="md:col-span-2">
                  <Button onClick={handleAddVerification} disabled={isSubmittingVerification}>
                    {isSubmittingVerification ? 'Saving...' : 'Add Verification'}
                  </Button>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Verification Timeline</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {verifications.length === 0 ? (
                  <div className="text-sm text-[var(--color-text-tertiary)]">No verifications recorded.</div>
                ) : (
                  verifications.map(({ usage, verification }) => (
                    <div key={verification.documentVerificationId} className="flex items-start gap-4 border-b border-[var(--color-border-light)] pb-4 last:border-b-0">
                      <div className="mt-1">
                        <CalendarClock className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      </div>
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <div className="text-sm font-medium text-[var(--color-text-primary)]">{verification.decision}</div>
                          <Badge variant="outline" className="text-xs">{usage.purpose}</Badge>
                        </div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">
                          {verification.verifierType} · {verification.verifierId || 'Unknown'} · {formatDateTime(verification.createdAt)}
                        </div>
                        {verification.decisionNotes && (
                          <div className="text-xs text-[var(--color-text-secondary)] mt-1">{verification.decisionNotes}</div>
                        )}
                        {verification.aiRunId && (
                          <div className="text-xs text-[var(--color-text-tertiary)] mt-1 flex items-center gap-2">
                            <Hash className="w-3.5 h-3.5" />
                            AI Run ID: {verification.aiRunId}
                          </div>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="versions">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Document Versions</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {document.versions.length === 0 ? (
                <div className="text-sm text-[var(--color-text-tertiary)]">No versions recorded.</div>
              ) : (
                document.versions.map((version) => (
                  <div key={version.documentVersionId} className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-4 last:border-b-0">
                    <div>
                      <div className="text-sm font-medium text-[var(--color-text-primary)]">Version {version.version}</div>
                      <div className="text-xs text-[var(--color-text-tertiary)]">Status: {version.status}</div>
                      <div className="text-xs text-[var(--color-text-tertiary)]">Submitted: {formatDateTime(version.submittedAt)}</div>
                    </div>
                    <div className="text-xs text-[var(--color-text-tertiary)] text-right">
                      Decisioned: {formatDateTime(version.decisionedAt)}
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
