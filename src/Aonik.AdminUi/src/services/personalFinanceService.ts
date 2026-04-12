import { api } from '@/lib/api';
import type {
  AdminBudgetResponse,
  CommitmentListResponse,
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

export interface AdminListTransactionsParams {
  personalAccountId?: string;
  category?: string;
  search?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export const personalFinanceService = {
  /* ------------------------------------------------------------------ */
  /*  Accounts (user-scoped, for self-serve context)                     */
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
  /*  Transactions (user-scoped)                                         */
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

  /* ------------------------------------------------------------------ */
  /*  Admin endpoints — scoped to a specific user                        */
  /* ------------------------------------------------------------------ */

  admin: {
    listAccounts: async (
      userId: string,
      includeArchived = false,
    ): Promise<PersonalAccountResponse[]> => {
      const query = includeArchived ? '?includeArchived=true' : '';
      return api.get<PersonalAccountResponse[]>(
        `/admin/personal-finance/users/${userId}/accounts${query}`,
      );
    },

    listTransactions: async (
      userId: string,
      params: AdminListTransactionsParams = {},
    ): Promise<PersonalTransactionResponse[]> => {
      const qp = new URLSearchParams();
      if (params.personalAccountId) qp.append('personalAccountId', params.personalAccountId);
      if (params.category) qp.append('category', params.category);
      if (params.search) qp.append('search', params.search);
      if (params.from) qp.append('from', params.from);
      if (params.to) qp.append('to', params.to);
      if (params.page) qp.append('page', params.page.toString());
      if (params.pageSize) qp.append('pageSize', params.pageSize.toString());
      const q = qp.toString();
      return api.get<PersonalTransactionResponse[]>(
        `/admin/personal-finance/users/${userId}/transactions${q ? `?${q}` : ''}`,
      );
    },

    listBudgets: async (userId: string): Promise<AdminBudgetResponse[]> => {
      return api.get<AdminBudgetResponse[]>(
        `/admin/personal-finance/users/${userId}/budgets`,
      );
    },

    listCommitments: async (
      userId: string,
      params: { status?: string; type?: string; page?: number; pageSize?: number } = {},
    ): Promise<CommitmentListResponse> => {
      const qp = new URLSearchParams();
      if (params.status) qp.append('status', params.status);
      if (params.type) qp.append('type', params.type);
      if (params.page) qp.append('page', params.page.toString());
      if (params.pageSize) qp.append('pageSize', params.pageSize.toString());
      const q = qp.toString();
      return api.get<CommitmentListResponse>(
        `/admin/personal-finance/users/${userId}/commitments${q ? `?${q}` : ''}`,
      );
    },

    getFinancialLifeGraph: async (userId: string): Promise<FinancialLifeGraphResponse> => {
      return api.get<FinancialLifeGraphResponse>(
        `/admin/personal-finance/users/${userId}/graph`,
      );
    },
  },
};

// ── Financial Life Graph Types ──────────────────────────────────────

export interface FinancialLifeGraphResponse {
  tenantId: string;
  userId: string;
  householdId: string | null;
  generatedAt: string;
  summary: FinancialLifeGraphSummary;
  nodes: FinancialLifeGraphNode[];
  edges: FinancialLifeGraphEdge[];
  sourceCoverage: { sourceType: string; count: number }[];
}

export interface FinancialLifeGraphSummary {
  accountsCount: number;
  linkedAccountsCount: number;
  transactionsCount: number;
  billsCount: number;
  goalsCount: number;
  subscriptionsCount: number;
  fundingRelationshipCount: number;
  inferredAnnotationCount: number;
  hasHousehold: boolean;
  householdMembersCount: number;
  relatedPartiesCount: number;
  partyId: string | null;
  householdId: string | null;
}

export interface FinancialLifeGraphNode {
  nodeId: string;
  nodeType: string;
  displayName: string;
  sourceType: string;
  sourceId: string | null;
  metadataJson: string | null;
}

export interface FinancialLifeGraphEdge {
  fromNodeId: string;
  predicate: string;
  toNodeId: string;
  metadataJson: string | null;
}
