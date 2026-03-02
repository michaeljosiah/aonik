import { apiGet, apiPatch, apiPost, apiPostForm } from "./client";

export type PersonalAccount = {
  personalAccountId: string;
  userId: string;
  householdId: string | null;
  name: string;
  accountType: string;
  currency: string;
  institutionName: string | null;
  externalReference: string | null;
  status: string;
  accountSubtype: string | null;
  last4: string | null;
  isArchived: boolean;
  openedAt: string | null;
  closedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
};

export type PersonalTransaction = {
  personalTransactionId: string;
  userId: string;
  personalAccountId: string | null;
  occurredAt: string;
  amount: number;
  currency: string;
  merchant: string | null;
  description: string | null;
  category: string | null;
  confidence: number;
  categorisedBy: string | null;
  classificationMethod: string | null;
  notes: string | null;
  tags: string[];
  createdAt: string;
  updatedAt: string | null;
};

export type StatementImport = {
  statementImportId: string;
  personalAccountId: string;
  fileName: string;
  format: string;
  status: string;
  rowsTotal: number;
  rowsParsed: number;
  rowsImported: number;
  rowsDuplicate: number;
  rowsFailed: number;
  failureReason: string | null;
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
};

export type ClassificationReviewItem = {
  personalTransactionId: string;
  personalAccountId: string | null;
  occurredAt: string;
  amount: number;
  currency: string;
  merchant: string | null;
  description: string | null;
  category: string | null;
  confidence: number;
  categorisedBy: string | null;
  classificationMethod: string | null;
  reviewStatus: string;
  createdAt: string;
  updatedAt: string | null;
};

export type SpendingSummary = {
  periodStart: string;
  periodEnd: string;
  currency: string;
  totalIncome: number;
  totalExpense: number;
  netAmount: number;
  transactionCount: number;
};

export type CategorySpendingItem = {
  category: string;
  totalAmount: number;
  percentage: number;
  transactionCount: number;
};

export type MerchantSpendingItem = {
  merchant: string;
  totalAmount: number;
  transactionCount: number;
};

export type AccountSpendingItem = {
  personalAccountId: string | null;
  accountName: string;
  totalAmount: number;
  transactionCount: number;
};

export const listPersonalAccounts = async (): Promise<PersonalAccount[]> => {
  return apiGet<PersonalAccount[]>('/personal-finance/accounts');
};

export const createPersonalAccount = async (request: {
  name: string;
  accountType: string;
  currency: string;
  institutionName?: string | null;
  externalReference?: string | null;
  accountSubtype?: string | null;
  last4?: string | null;
}): Promise<PersonalAccount> => {
  return apiPost<PersonalAccount>('/personal-finance/accounts', request);
};

export const listPersonalTransactions = async (): Promise<PersonalTransaction[]> => {
  return apiGet<PersonalTransaction[]>('/personal-finance/transactions');
};

export const createPersonalTransaction = async (request: {
  personalAccountId?: string | null;
  occurredAt: string;
  amount: number;
  currency: string;
  merchant?: string | null;
  description?: string | null;
  category?: string | null;
  notes?: string | null;
  tags?: string[];
}): Promise<PersonalTransaction> => {
  return apiPost<PersonalTransaction>('/personal-finance/transactions', request);
};

export const uploadStatement = async (personalAccountId: string, file: File): Promise<StatementImport> => {
  const form = new FormData();
  form.append('personalAccountId', personalAccountId);
  form.append('files', file, file.name);
  return apiPostForm<StatementImport>('/personal-finance/imports/statements', form);
};

export const listStatementImports = async (): Promise<StatementImport[]> => {
  return apiGet<StatementImport[]>('/personal-finance/imports/statements');
};

export const applyStatementImport = async (statementImportId: string): Promise<{ rowsImported: number }> => {
  return apiPost<{ rowsImported: number }>(`/personal-finance/imports/statements/${statementImportId}/apply`, {});
};

export const getReviewQueue = async (): Promise<ClassificationReviewItem[]> => {
  return apiGet<ClassificationReviewItem[]>('/personal-finance/classification/review-queue');
};

export const overrideClassification = async (
  transactionId: string,
  category: string,
  createRuleFromCorrection = false
): Promise<ClassificationReviewItem> => {
  return apiPost<ClassificationReviewItem>(`/personal-finance/classification/review/${transactionId}/override`, {
    category,
    notes: null,
    createRuleFromCorrection,
    rulePattern: null,
    rulePriority: 100,
    ruleMatchType: 'contains'
  });
};

export const acceptClassification = async (transactionId: string): Promise<ClassificationReviewItem> => {
  return apiPost<ClassificationReviewItem>(`/personal-finance/classification/review/${transactionId}/accept`, {});
};

export const getSpendingSummary = async (periodStart: string, periodEnd: string): Promise<SpendingSummary> => {
  const query = new URLSearchParams({ periodStart, periodEnd });
  return apiGet<SpendingSummary>(`/personal-finance/insights/spending-summary?${query.toString()}`);
};

export const getCategoryBreakdown = async (periodStart: string, periodEnd: string): Promise<CategorySpendingItem[]> => {
  const query = new URLSearchParams({ periodStart, periodEnd });
  return apiGet<CategorySpendingItem[]>(`/personal-finance/insights/category-breakdown?${query.toString()}`);
};

export const getMerchantBreakdown = async (periodStart: string, periodEnd: string): Promise<MerchantSpendingItem[]> => {
  const query = new URLSearchParams({ periodStart, periodEnd });
  return apiGet<MerchantSpendingItem[]>(`/personal-finance/insights/merchant-breakdown?${query.toString()}`);
};

export const getAccountBreakdown = async (periodStart: string, periodEnd: string): Promise<AccountSpendingItem[]> => {
  const query = new URLSearchParams({ periodStart, periodEnd });
  return apiGet<AccountSpendingItem[]>(`/personal-finance/insights/account-breakdown?${query.toString()}`);
};

export const updatePersonalTransactionCategory = async (
  transaction: PersonalTransaction,
  category: string | null
): Promise<PersonalTransaction> => {
  return apiPatch<PersonalTransaction>(`/personal-finance/transactions/${transaction.personalTransactionId}`, {
    personalAccountId: transaction.personalAccountId,
    occurredAt: transaction.occurredAt,
    amount: transaction.amount,
    currency: transaction.currency,
    merchant: transaction.merchant,
    description: transaction.description,
    category,
    notes: transaction.notes,
    tags: transaction.tags
  });
};
