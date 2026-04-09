import { api } from '@/lib/api';
import type {
  CreateManualPersonalTransactionRequest,
  CreatePersonalAccountRequest,
  PersonalAccountResponse,
  PersonalTransactionResponse,
  TransactionCategoryListResponse,
} from '@/types';

export interface ListTransactionsParams {
  from?: string;
  to?: string;
  personalAccountId?: string;
  category?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export const personalFinanceService = {
  /* ------------------------------------------------------------------ */
  /*  Accounts                                                            */
  /* ------------------------------------------------------------------ */

  listAccounts: async (includeArchived = false): Promise<PersonalAccountResponse[]> => {
    const query = includeArchived ? '?includeArchived=true' : '';
    return api.get<PersonalAccountResponse[]>(`/personal-finance/accounts${query}`);
  },

  createAccount: async (
    data: CreatePersonalAccountRequest,
  ): Promise<PersonalAccountResponse> => {
    return api.post<PersonalAccountResponse>('/personal-finance/accounts', data);
  },

  /* ------------------------------------------------------------------ */
  /*  Transactions                                                        */
  /* ------------------------------------------------------------------ */

  listTransactions: async (
    params: ListTransactionsParams = {},
  ): Promise<PersonalTransactionResponse[]> => {
    const qp = new URLSearchParams();
    if (params.from) qp.append('from', params.from);
    if (params.to) qp.append('to', params.to);
    if (params.personalAccountId) qp.append('personalAccountId', params.personalAccountId);
    if (params.category) qp.append('category', params.category);
    if (params.search) qp.append('search', params.search);
    if (params.page) qp.append('page', params.page.toString());
    if (params.pageSize) qp.append('pageSize', params.pageSize.toString());
    const q = qp.toString();
    return api.get<PersonalTransactionResponse[]>(
      `/personal-finance/transactions${q ? `?${q}` : ''}`,
    );
  },

  createTransaction: async (
    data: CreateManualPersonalTransactionRequest,
  ): Promise<PersonalTransactionResponse> => {
    return api.post<PersonalTransactionResponse>('/personal-finance/transactions', data);
  },

  /* ------------------------------------------------------------------ */
  /*  Categories                                                          */
  /* ------------------------------------------------------------------ */

  listCategories: async (): Promise<TransactionCategoryListResponse> => {
    return api.get<TransactionCategoryListResponse>('/personal-finance/categories');
  },
};
