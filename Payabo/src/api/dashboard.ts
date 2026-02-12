import { apiGet } from "./client";
import { getPublicBillPaymentDraft } from "./orders";
import { listPaymentHistoryForUser, type PaymentHistoryItem } from "../pages/payments/paymentHistory";

import { draftOrderIdStorageKey } from "../pages/payments/draftIntent";

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

const formatAmount = (amount: number | null, currency: string): string => {
  if (amount == null) {
    return "-";
  }

  try {
    return new Intl.NumberFormat("en-GB", {
      style: "currency",
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
};

const formatDateLabel = (isoDate: string): string => {
  try {
    return new Intl.DateTimeFormat("en-GB", {
      day: "2-digit",
      month: "short",
      year: "numeric"
    }).format(new Date(isoDate));
  } catch {
    return isoDate;
  }
};

const mapHistoryToRecent = (items: PaymentHistoryItem[]): DashboardRecentTransaction[] => {
  return items.slice(0, 8).map((item) => ({
    id: item.id,
    billerName: item.billerName ?? "Provider",
    serviceName: item.serviceName,
    dateLabel: formatDateLabel(item.createdAt),
    amountLabel: formatAmount(item.amount, item.currency),
    status: item.status
  }));
};

const mapHistoryToUpcoming = (items: PaymentHistoryItem[]): DashboardUpcomingBill[] => {
  return items
    .filter((item) => item.status.toLowerCase() === "pending" || item.orderStatus.toLowerCase() === "pending")
    .slice(0, 5)
    .map((item) => ({
      id: item.id,
      billerName: item.billerName ?? "Provider",
      serviceName: item.serviceName,
      dueDate: formatDateLabel(item.createdAt),
      amountLabel: formatAmount(item.amount, item.currency)
    }));
};

const getDraftUpcomingBill = async (): Promise<DashboardUpcomingBill | null> => {
  const draftOrderId = sessionStorage.getItem(draftOrderIdStorageKey);
  if (!draftOrderId) {
    return null;
  }

  try {
    const draft = await getPublicBillPaymentDraft(draftOrderId);
    return {
      id: draft.orderId,
      billerName: draft.billerName ?? "Provider",
      serviceName: draft.serviceName,
      dueDate: formatDateLabel(draft.createdAt),
      amountLabel: formatAmount(draft.requestedAmount, draft.currency)
    };
  } catch {
    return null;
  }
};

export const getDashboardSummary = async (userId: string): Promise<DashboardSummaryResponse> => {
  try {
    return await apiGet<DashboardSummaryResponse>(`/public/dashboard/summary?userId=${encodeURIComponent(userId)}`);
  } catch {
    const historyItems = listPaymentHistoryForUser(userId);
    const recentTransactions = mapHistoryToRecent(historyItems);
    const upcomingBills = mapHistoryToUpcoming(historyItems);
    const draftUpcomingBill = await getDraftUpcomingBill();

    const mergedUpcomingBills = draftUpcomingBill
      ? [draftUpcomingBill, ...upcomingBills.filter((item) => item.id !== draftUpcomingBill.id)]
      : upcomingBills;

    return {
      upcomingBills: mergedUpcomingBills.slice(0, 5),
      recentTransactions
    };
  }
};
