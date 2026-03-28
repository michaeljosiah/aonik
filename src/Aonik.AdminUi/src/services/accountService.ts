import { api } from '@/lib/api';
import type {
  ExternalAccountConnectionResponse,
  ExternalAccountLinkSessionResponse,
  ExternalAccountLinkExchangeResponse,
  ExternalAccountTransactionResponse,
  ExternalAccountTransactionSyncResponse,
  ExternalAccountResponse,
  CreateExternalAccountRequest,
  CreateExternalAccountTransactionRequest,
  ExternalAccountTransactionAttachmentResponse,
  PagedResult,
} from '@/types';

export interface ListExternalAccountTransactionsParams {
  externalAccountId?: string;
  connectionId?: string;
  reconciliationStatus?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

export const externalAccountService = {
  listConnections: async (includeDisconnected = false): Promise<ExternalAccountConnectionResponse[]> =>
    api.get<ExternalAccountConnectionResponse[]>(
      `/admin/external-accounts/connections?includeDisconnected=${includeDisconnected}`
    ),

  createSession: async (data: { provider: string; mode?: string; connectionId?: string }): Promise<ExternalAccountLinkSessionResponse> =>
    api.post<ExternalAccountLinkSessionResponse>('/admin/external-accounts/connections/sessions', data),

  exchangeSession: async (data: { sessionId: string; temporaryCode: string }): Promise<ExternalAccountLinkExchangeResponse> =>
    api.post<ExternalAccountLinkExchangeResponse>('/admin/external-accounts/connections/exchanges', data),

  refreshConnection: async (connectionId: string): Promise<ExternalAccountConnectionResponse> =>
    api.post<ExternalAccountConnectionResponse>(`/admin/external-accounts/connections/${connectionId}/refresh`),

  disconnectConnection: async (connectionId: string): Promise<ExternalAccountConnectionResponse> =>
    api.post<ExternalAccountConnectionResponse>(`/admin/external-accounts/connections/${connectionId}/disconnect`),

  syncTransactions: async (connectionId: string): Promise<ExternalAccountTransactionSyncResponse> =>
    api.post<ExternalAccountTransactionSyncResponse>(`/admin/external-accounts/connections/${connectionId}/transactions/sync`),

  listTransactions: async (params: ListExternalAccountTransactionsParams = {}): Promise<PagedResult<ExternalAccountTransactionResponse>> => {
    const queryParams = new URLSearchParams();
    if (params.externalAccountId) queryParams.append('externalAccountId', params.externalAccountId);
    if (params.connectionId) queryParams.append('connectionId', params.connectionId);
    if (params.reconciliationStatus) queryParams.append('reconciliationStatus', params.reconciliationStatus);
    if (params.from) queryParams.append('from', params.from);
    if (params.to) queryParams.append('to', params.to);
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    const query = queryParams.toString();
    return api.get<PagedResult<ExternalAccountTransactionResponse>>(
      `/admin/external-accounts/transactions${query ? `?${query}` : ''}`
    );
  },

  createAccount: async (data: CreateExternalAccountRequest): Promise<ExternalAccountResponse> =>
    api.post<ExternalAccountResponse>('/admin/external-accounts', data),

  listAccounts: async (): Promise<ExternalAccountResponse[]> =>
    api.get<ExternalAccountResponse[]>('/admin/external-accounts'),

  createTransaction: async (data: CreateExternalAccountTransactionRequest): Promise<ExternalAccountTransactionResponse> =>
    api.post<ExternalAccountTransactionResponse>('/admin/external-accounts/transactions', data),

  uploadAttachment: async (transactionId: string, file: File): Promise<ExternalAccountTransactionAttachmentResponse> => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post<ExternalAccountTransactionAttachmentResponse>(
      `/admin/external-accounts/transactions/${transactionId}/attachments`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
  },

  listAttachments: async (transactionId: string): Promise<ExternalAccountTransactionAttachmentResponse[]> =>
    api.get<ExternalAccountTransactionAttachmentResponse[]>(
      `/admin/external-accounts/transactions/${transactionId}/attachments`
    ),

  deleteAttachment: async (attachmentId: string): Promise<void> =>
    api.delete<void>(`/admin/external-accounts/attachments/${attachmentId}`),
};
