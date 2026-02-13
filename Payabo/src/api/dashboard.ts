import { apiGet } from "./client";

export type DashboardUpcomingBill = {
  id: string;
  billerName: string;
  serviceName: string;
  dueDate: string;
  amountLabel: string;
};

export type DashboardRecentTransaction = {
  id: string;
  billerName: string;
  serviceName: string;
  dateLabel: string;
  amountLabel: string;
  status: string;
};

type DashboardSummaryResponse = {
  upcomingBills: DashboardUpcomingBill[];
  recentTransactions: DashboardRecentTransaction[];
};

export const getDashboardSummary = async (userId: string): Promise<DashboardSummaryResponse> => {
  return await apiGet<DashboardSummaryResponse>(`/public/dashboard/summary?userId=${encodeURIComponent(userId)}`);
};

export const getRecentTransactions = async (userId: string): Promise<DashboardRecentTransaction[]> => {
  const summary = await getDashboardSummary(userId);
  return summary.recentTransactions;
};

export const getRecentTransactionById = async (
  userId: string,
  transactionId: string
): Promise<DashboardRecentTransaction | null> => {
  const transactions = await getRecentTransactions(userId);
  return transactions.find((item) => item.id === transactionId) ?? null;
};
