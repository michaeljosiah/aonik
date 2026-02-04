import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { FilePlus, ArrowLeft } from 'lucide-react';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { documentService } from '@/services/documentService';
import type { AddDocumentFileRequest, AddDocumentUsageRequest, CreateDocumentRequest } from '@/types';

export function DocumentCreatePage() {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [documentForm, setDocumentForm] = useState<CreateDocumentRequest>({
    ownerPartyId: '',
    documentType: '',
    status: '',
    issuedOn: '',
    expiresOn: '',
    issuerName: '',
    countryCode: '',
    referenceNumber: '',
    tags: [],
    attributesJson: '',
  });
  const [tagInput, setTagInput] = useState('');
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

  const handleSubmit = async () => {
    if (!documentForm.ownerPartyId || !documentForm.documentType) {
      toast.error('Owner party ID and document type are required.');
      return;
    }
    setIsSubmitting(true);
    try {
      const tags = tagInput
        .split(',')
        .map((tag) => tag.trim())
        .filter((tag) => tag.length > 0);

      const documentPayload: CreateDocumentRequest = {
        ownerPartyId: documentForm.ownerPartyId,
        documentType: documentForm.documentType,
        status: documentForm.status || undefined,
        issuedOn: documentForm.issuedOn || undefined,
        expiresOn: documentForm.expiresOn || undefined,
        issuerName: documentForm.issuerName || undefined,
        countryCode: documentForm.countryCode || undefined,
        referenceNumber: documentForm.referenceNumber || undefined,
        tags,
        attributesJson: documentForm.attributesJson || undefined,
      };

      const created = await documentService.create(documentPayload);

      const hasFilePayload = fileForm.storageProvider && fileForm.storageKey && fileForm.contentType;
      if (hasFilePayload) {
        await documentService.addFile(created.documentId, {
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
          metadataJson: fileForm.metadataJson || undefined,
        });
      }

      const hasUsagePayload = usageForm.purpose;
      if (hasUsagePayload) {
        await documentService.addUsage(created.documentId, {
          ownerPartyId: usageForm.ownerPartyId || created.ownerPartyId,
          purpose: usageForm.purpose,
          relatedEntityType: usageForm.relatedEntityType || undefined,
          relatedEntityId: usageForm.relatedEntityId || undefined,
          status: usageForm.status || undefined,
          notes: usageForm.notes || undefined,
        });
      }

      toast.success('Document created.');
      navigate(`/compliance/documents/${created.documentId}`);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      toast.error(message || 'Failed to create document.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Compliance', href: '/compliance' },
          { label: 'Documents', href: '/compliance/documents' },
          { label: 'Create', icon: <FilePlus className="w-3.5 h-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon-sm" onClick={() => navigate('/compliance/documents')}>
            <ArrowLeft className="w-4 h-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Create Document</h1>
            <p className="text-[var(--color-text-secondary)]">
              Register a document and optionally attach files and usage context.
            </p>
          </div>
        </div>
        <Button onClick={handleSubmit} disabled={isSubmitting}>
          {isSubmitting ? 'Saving...' : 'Create Document'}
        </Button>
      </div>

      <div className="grid gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Document Metadata</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2 text-sm">
            <Input
              value={documentForm.ownerPartyId}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, ownerPartyId: event.target.value }))}
              placeholder="Owner party ID"
            />
            <Input
              value={documentForm.documentType}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, documentType: event.target.value }))}
              placeholder="Document type"
            />
            <Input
              value={documentForm.status ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, status: event.target.value }))}
              placeholder="Status (Draft, Pending)"
            />
            <Input
              value={documentForm.issuerName ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, issuerName: event.target.value }))}
              placeholder="Issuer name"
            />
            <Input
              value={documentForm.countryCode ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, countryCode: event.target.value }))}
              placeholder="Country code"
            />
            <Input
              value={documentForm.referenceNumber ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, referenceNumber: event.target.value }))}
              placeholder="Reference number"
            />
            <Input
              value={documentForm.issuedOn ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, issuedOn: event.target.value }))}
              placeholder="Issued on (ISO date)"
            />
            <Input
              value={documentForm.expiresOn ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, expiresOn: event.target.value }))}
              placeholder="Expires on (ISO date)"
            />
            <Input
              value={tagInput}
              onChange={(event) => setTagInput(event.target.value)}
              placeholder="Tags (comma-separated)"
              className="md:col-span-2"
            />
            <Textarea
              value={documentForm.attributesJson ?? ''}
              onChange={(event) => setDocumentForm((prev) => ({ ...prev, attributesJson: event.target.value }))}
              placeholder="Attributes JSON"
              className="md:col-span-2"
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Initial File (Optional)</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2 text-sm">
            <Input
              value={fileForm.storageProvider}
              onChange={(event) => setFileForm((prev) => ({ ...prev, storageProvider: event.target.value }))}
              placeholder="Storage provider"
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
              value={fileForm.side ?? ''}
              onChange={(event) => setFileForm((prev) => ({ ...prev, side: event.target.value }))}
              placeholder="Side (front/back)"
            />
            <Input
              value={fileForm.capturedAt ?? ''}
              onChange={(event) => setFileForm((prev) => ({ ...prev, capturedAt: event.target.value }))}
              placeholder="Captured at (ISO)"
            />
            <Textarea
              value={fileForm.metadataJson ?? ''}
              onChange={(event) => setFileForm((prev) => ({ ...prev, metadataJson: event.target.value }))}
              placeholder="Metadata JSON"
              className="md:col-span-2"
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Usage Context (Optional)</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2 text-sm">
            <Input
              value={usageForm.ownerPartyId}
              onChange={(event) => setUsageForm((prev) => ({ ...prev, ownerPartyId: event.target.value }))}
              placeholder="Owner party ID (defaults to document owner)"
            />
            <Input
              value={usageForm.purpose}
              onChange={(event) => setUsageForm((prev) => ({ ...prev, purpose: event.target.value }))}
              placeholder="Purpose (KYC, KYB, Compliance)"
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
              placeholder="Status"
            />
            <Textarea
              value={usageForm.notes ?? ''}
              onChange={(event) => setUsageForm((prev) => ({ ...prev, notes: event.target.value }))}
              placeholder="Notes"
              className="md:col-span-2"
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
