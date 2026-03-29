import { api } from '@/lib/api';
import type {
  AccountConnectionResponse,
  AccountLinkSessionResponse,
  AccountLinkExchangeResponse,
  AccountTransactionResponse,
  AccountTransactionSyncResponse,
  AccountResponse,
  CreateAccountRequest,
  CreateAccountTransactionRequest,
  AccountTransactionAttachmentResponse,
  PagedResult,
} from '@/types';

export interface ListAccountTransactionsParams {
  accountId?: string;
  connectionId?: string;
  reconciliationStatus?: string;
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

export const accountService = {
  listConnections: async (includeDisconnected = false): Promise<AccountConnectionResponse[]> =>
    api.get<AccountConnectionResponse[]>(
      `/admin/accounts/connections?includeDisconnected=${includeDisconnected}`
    ),

  createSession: async (data: { provider: string; mode?: string; connectionId?: string }): Promise<AccountLinkSessionResponse> =>
    api.post<AccountLinkSessionResponse>('/admin/accounts/connections/sessions', data),

  exchangeSession: async (data: { sessionId: string; temporaryCode: string }): Promise<AccountLinkExchangeResponse> =>
    api.post<AccountLinkExchangeResponse>('/admin/accounts/connections/exchanges', data),

  refreshConnection: async (connectionId: string): Promise<AccountConnectionResponse> =>
    api.post<AccountConnectionResponse>(`/admin/accounts/connections/${connectionId}/refresh`),

  disconnectConnection: async (connectionId: string): Promise<AccountConnectionResponse> =>
    api.post<AccountConnectionResponse>(`/admin/accounts/connections/${connectionId}/disconnect`),

  syncTransactions: async (connectionId: string): Promise<AccountTransactionSyncResponse> =>
    api.post<AccountTransactionSyncResponse>(`/admin/accounts/connections/${connectionId}/transactions/sync`),

  listTransactions: async (params: ListAccountTransactionsParams = {}): Promise<PagedResult<AccountTransactionResponse>> => {
    const queryParams = new URLSearchParams();
    if (params.accountId) queryParams.append('accountId', params.accountId);
    if (params.connectionId) queryParams.append('connectionId', params.connectionId);
    if (params.reconciliationStatus) queryParams.append('reconciliationStatus', params.reconciliationStatus);
    if (params.from) queryParams.append('from', params.from);
    if (params.to) queryParams.append('to', params.to);
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    const query = queryParams.toString();
    return api.get<PagedResult<AccountTransactionResponse>>(
      `/admin/accounts/transactions${query ? `?${query}` : ''}`
    );
  },

  createAccount: async (data: CreateAccountRequest): Promise<AccountResponse> =>
    api.post<AccountResponse>('/admin/accounts', data),

  listAccounts: async (): Promise<AccountResponse[]> =>
    api.get<AccountResponse[]>('/admin/accounts'),

  createTransaction: async (data: CreateAccountTransactionRequest): Promise<AccountTransactionResponse> =>
    api.post<AccountTransactionResponse>('/admin/accounts/transactions', data),

  uploadAttachment: async (transactionId: string, file: File): Promise<AccountTransactionAttachmentResponse> => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post<AccountTransactionAttachmentResponse>(
      `/admin/accounts/transactions/${transactionId}/attachments`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
  },

  listAttachments: async (transactionId: string): Promise<AccountTransactionAttachmentResponse[]> =>
    api.get<AccountTransactionAttachmentResponse[]>(
      `/admin/accounts/transactions/${transactionId}/attachments`
    ),

  deleteAttachment: async (attachmentId: string): Promise<void> =>
    api.delete<void>(`/admin/accounts/attachments/${attachmentId}`),
};
