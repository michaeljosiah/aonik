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
}

export const documentService = {
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

    const query = queryParams.toString();
    return api.get<PagedResult<DocumentListItem>>(`/compliance/documents${query ? `?${query}` : ''}`);
  },
  get: async (documentId: string): Promise<DocumentDetailsResponse> => {
    return api.get<DocumentDetailsResponse>(`/compliance/documents/${documentId}`);
  },
  create: async (data: CreateDocumentRequest): Promise<DocumentResponse> => {
    return api.post<DocumentResponse>('/compliance/documents', data);
  },
  addFile: async (documentId: string, data: AddDocumentFileRequest): Promise<DocumentFileResponse> => {
    return api.post<DocumentFileResponse>(`/compliance/documents/${documentId}/files`, data);
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
