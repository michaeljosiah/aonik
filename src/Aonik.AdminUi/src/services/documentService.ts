import { api } from '@/lib/api';
import type {
  AddDocumentFileRequest,
  AddDocumentUsageRequest,
  AddDocumentVerificationRequest,
  CreateDocumentRequest,
  DocumentDetailsResponse,
  DocumentListItem,
  DocumentResponse,
  DocumentFileResponse,
  DocumentUsageResponse,
  DocumentVerificationResponse,
  PagedResult,
} from '@/types';

export interface ListDocumentsParams {
  pageNumber?: number;
  pageSize?: number;
  documentType?: string;
  status?: string;
  ownerPartyId?: string;
  countryCode?: string;
  issuedFrom?: string;
  issuedTo?: string;
  expiresFrom?: string;
  expiresTo?: string;
  tag?: string;
  usagePurpose?: string;
  search?: string;
  relatedEntityType?: string;
  relatedEntityId?: string;
}

export interface FinancialExtractionReceipt { extractionId: string; documentId: string; candidateHash: string; status: string; issues: string[]; aiRunId: string }
export interface FinancialDocumentCandidate extends FinancialExtractionReceipt {
  fileId: string; sourceHash: string; modelId: string; promptHash: string;
  fields: { kind: string; invoiceNumber: string; counterpartyName: string; currency: string; dueUtc: string | null;
    lines: { description: string; quantity: number; unitPrice: number; total: number }[]; subtotal: number; tax: number; discount: number; total: number; confidence: number; evidence: string[] };
  extractor: { extractor: string; version: string; pages: number; confidence: number | null } | null;
  source: { connectionId: string; workflowId: string; workflowVersionId: string; workflowGrantId: string; bindingId: string; bindingRevision: number } | null;
}

export const documentService = {
  financialExtractions: (id: string, signal?: AbortSignal) => api.get<FinancialExtractionReceipt[]>(`/documents/${encodeURIComponent(id)}/financial-extractions`, { signal }),
  financialCandidate: (id: string, signal?: AbortSignal) => api.get<FinancialDocumentCandidate>(`/documents/financial-extractions/${encodeURIComponent(id)}`, { signal }),
  financialContent: (documentId: string, fileId: string) => api.get<Blob>(`/documents/${encodeURIComponent(documentId)}/files/${encodeURIComponent(fileId)}/financial-content`, { responseType: 'blob' }),
  list: async (params: ListDocumentsParams = {}): Promise<PagedResult<DocumentListItem>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.documentType) queryParams.append('documentType', params.documentType);
    if (params.status) queryParams.append('status', params.status);
    if (params.ownerPartyId) queryParams.append('ownerPartyId', params.ownerPartyId);
    if (params.countryCode) queryParams.append('countryCode', params.countryCode);
    if (params.issuedFrom) queryParams.append('issuedFrom', params.issuedFrom);
    if (params.issuedTo) queryParams.append('issuedTo', params.issuedTo);
    if (params.expiresFrom) queryParams.append('expiresFrom', params.expiresFrom);
    if (params.expiresTo) queryParams.append('expiresTo', params.expiresTo);
    if (params.tag) queryParams.append('tag', params.tag);
    if (params.usagePurpose) queryParams.append('usagePurpose', params.usagePurpose);
    if (params.search) queryParams.append('search', params.search);
    if (params.relatedEntityType) queryParams.append('relatedEntityType', params.relatedEntityType);
    if (params.relatedEntityId) queryParams.append('relatedEntityId', params.relatedEntityId);

    const query = queryParams.toString();
    // Spec 035 — generic document read/write re-homed from /compliance/documents to /documents.
    return api.get<PagedResult<DocumentListItem>>(`/documents${query ? `?${query}` : ''}`);
  },
  get: async (documentId: string): Promise<DocumentDetailsResponse> => {
    // Spec 035 — the decoupled Documents module returns the flat document metadata
    // (DocumentDto) from GET /documents/{id} and its attached files from a separate
    // GET /documents/{id}/files; it has no compliance usages/versions. Compose them into
    // the composite shape the detail page expects. Owner-scoping is enforced server-side,
    // so a customer only sees their own document and its files.
    const [document, files] = await Promise.all([
      api.get<DocumentResponse>(`/documents/${documentId}`),
      api.get<DocumentFileResponse[]>(`/documents/${documentId}/files`),
    ]);
    return { document, files, usages: [], versions: [] };
  },
  create: async (data: CreateDocumentRequest): Promise<DocumentResponse> => {
    return api.post<DocumentResponse>('/documents', data);
  },
  addFile: async (documentId: string, data: AddDocumentFileRequest): Promise<DocumentFileResponse> => {
    return api.post<DocumentFileResponse>(`/compliance/documents/${documentId}/files`, data);
  },
  uploadFile: async (
    documentId: string,
    data: {
      file: File;
      pageIndex?: number;
      side?: string;
      capturedAt?: string;
      capturedBy?: string;
      metadataJson?: string;
    }
  ): Promise<DocumentFileResponse> => {
    const formData = new FormData();
    formData.append('file', data.file);

    if (data.pageIndex !== undefined) formData.append('pageIndex', data.pageIndex.toString());
    if (data.side) formData.append('side', data.side);
    if (data.capturedAt) formData.append('capturedAt', data.capturedAt);
    if (data.capturedBy) formData.append('capturedBy', data.capturedBy);
    if (data.metadataJson) formData.append('metadataJson', data.metadataJson);

    return api.post<DocumentFileResponse>(`/documents/${documentId}/files`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
  },
  addUsage: async (documentId: string, data: AddDocumentUsageRequest): Promise<DocumentUsageResponse> => {
    return api.post<DocumentUsageResponse>(`/compliance/documents/${documentId}/usages`, data);
  },
  addVerification: async (
    documentUsageId: string,
    data: AddDocumentVerificationRequest
  ): Promise<DocumentVerificationResponse> => {
    return api.post<DocumentVerificationResponse>(`/compliance/document-usages/${documentUsageId}/verifications`, data);
  },
};
