import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import {
  ArrowLeft,
  CloudUpload,
  FileText,
  Trash2,
  X,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { documentService } from '@/services/documentService';

/* -------------------------------------------------------------------------- */
/*  Constants                                                                  */
/* -------------------------------------------------------------------------- */

const DOCUMENT_TYPES = [
  'National ID',
  'Passport',
  'Driving Licence',
  'Proof of Address',
  'Bank Statement',
  'Utility Bill',
  'Tax Certificate',
  'Certificate of Incorporation',
  'Business Licence',
  'Power of Attorney',
  'Other',
] as const;

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function fileIcon(contentType: string) {
  if (contentType.startsWith('image/')) return 'image';
  if (contentType === 'application/pdf') return 'pdf';
  return 'file';
}

/* -------------------------------------------------------------------------- */
/*  Component                                                                  */
/* -------------------------------------------------------------------------- */

export function DocumentCreatePage() {
  const navigate = useNavigate();

  // Form state
  const [ownerPartyId, setOwnerPartyId] = useState('');
  const [documentType, setDocumentType] = useState('');
  const [customType, setCustomType] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const resolvedType = documentType === 'Other' ? customType.trim() : documentType;

  /* ---------------------------------------------------------------------- */
  /*  File handling                                                          */
  /* ---------------------------------------------------------------------- */

  const addFiles = useCallback((incoming: FileList | File[]) => {
    const newFiles = Array.from(incoming);
    setFiles((prev) => {
      // Deduplicate by name + size
      const existing = new Set(prev.map((f) => `${f.name}:${f.size}`));
      const unique = newFiles.filter((f) => !existing.has(`${f.name}:${f.size}`));
      return [...prev, ...unique];
    });
  }, []);

  const removeFile = useCallback((index: number) => {
    setFiles((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      if (e.dataTransfer.files.length > 0) {
        addFiles(e.dataTransfer.files);
      }
    },
    [addFiles],
  );

  /* ---------------------------------------------------------------------- */
  /*  Submit                                                                  */
  /* ---------------------------------------------------------------------- */

  const handleSubmit = async () => {
    if (!ownerPartyId.trim()) {
      toast.error('Owner party ID is required.');
      return;
    }
    if (!resolvedType) {
      toast.error('Please select a document type.');
      return;
    }

    setIsSubmitting(true);
    try {
      const created = await documentService.create({
        ownerPartyId: ownerPartyId.trim(),
        documentType: resolvedType,
        status: 'Draft',
      });

      // Upload files sequentially
      for (const file of files) {
        await documentService.uploadFile(created.documentId, { file });
      }

      toast.success(
        files.length > 0
          ? `Document created with ${files.length} file${files.length > 1 ? 's' : ''}.`
          : 'Document created.',
      );
      navigate(`/compliance/documents/${created.documentId}`);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to create document.');
    } finally {
      setIsSubmitting(false);
    }
  };

  /* ---------------------------------------------------------------------- */
  /*  Render                                                                  */
  /* ---------------------------------------------------------------------- */

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Compliance', href: '/compliance' },
          { label: 'Documents', href: '/compliance/documents' },
          { label: 'New Document' },
        ]}
        className="mb-4"
      />

      {/* Header */}
      <div className="mb-8 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon-sm" onClick={() => navigate('/compliance/documents')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">New Document</h1>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Choose a type and attach your files.
            </p>
          </div>
        </div>
        <Button onClick={handleSubmit} disabled={isSubmitting || !resolvedType || !ownerPartyId.trim()}>
          {isSubmitting ? 'Creating...' : 'Create Document'}
        </Button>
      </div>

      <div className="mx-auto max-w-[48rem] space-y-6">
        {/* Document info */}
        <Card>
          <CardContent className="p-6 space-y-5">
            <div className="space-y-1.5">
              <Label htmlFor="owner-party">Owner Party ID</Label>
              <Input
                id="owner-party"
                value={ownerPartyId}
                onChange={(e) => setOwnerPartyId(e.target.value)}
                placeholder="e.g. party_01HXYZ..."
              />
              <p className="text-xs text-[var(--color-text-tertiary)]">
                The customer or entity this document belongs to.
              </p>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="doc-type">Document Type</Label>
              <Select value={documentType} onValueChange={setDocumentType}>
                <SelectTrigger id="doc-type">
                  <SelectValue placeholder="Select a type..." />
                </SelectTrigger>
                <SelectContent>
                  {DOCUMENT_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {type}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {documentType === 'Other' && (
              <div className="space-y-1.5">
                <Label htmlFor="custom-type">Custom Type</Label>
                <Input
                  id="custom-type"
                  value={customType}
                  onChange={(e) => setCustomType(e.target.value)}
                  placeholder="Enter a custom document type"
                />
              </div>
            )}
          </CardContent>
        </Card>

        {/* File upload */}
        <Card>
          <CardContent className="p-6">
            <Label className="mb-3 block">Files</Label>

            {/* Drop zone */}
            <div
              className={`relative flex flex-col items-center justify-center rounded-xl border-2 border-dashed px-6 py-10 transition-colors ${
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
              <CloudUpload className="mb-3 h-8 w-8 text-[var(--color-text-tertiary)]" />
              <p className="mb-1 text-sm font-medium text-[var(--color-text-primary)]">
                Drag & drop files here
              </p>
              <p className="mb-3 text-xs text-[var(--color-text-tertiary)]">
                or click to browse from your computer
              </p>
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

            {/* File list */}
            {files.length > 0 && (
              <div className="mt-4 space-y-2">
                {files.map((file, index) => {
                  const kind = fileIcon(file.type);
                  return (
                    <div
                      key={`${file.name}-${file.size}`}
                      className="flex items-center gap-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/40 px-4 py-3"
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
                      <Badge variant="secondary" className="text-xs capitalize">
                        {kind}
                      </Badge>
                      <button
                        type="button"
                        onClick={() => removeFile(index)}
                        className="ml-1 rounded-md p-1 text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-error)] transition-colors"
                        title="Remove file"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
