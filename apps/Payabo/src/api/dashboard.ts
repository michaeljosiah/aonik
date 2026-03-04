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

type OrdersPagedResponse = {
  items: OrderListItemResponse[];
};

type OrderListItemResponse = {
  orderId: string;
  status: string;
  originCurrency: string;
  totalAmountIn: number;
  createdAt: string;
  updatedAt: string | null;
};

type OrderDetailResponse = {
  orderId: string;
  status: string;
  originCurrency: string;
  totalAmountIn: number;
  createdAt: string;
  submittedAt: string | null;
  items: Array<{
    billerName: string;
    serviceName: string;
  }>;
};

const billPaymentOrderType = "BillPayment";
const dashboardOrderPageSize = 20;
const recentTransactionsLimit = 10;
const upcomingBillsLimit = 5;

const terminalStatuses = new Set([
  "complete",
  "completed",
  "cancelled",
  "canceled",
  "failed",
  "expired"
]);

const formatDateLabel = (value: string | null | undefined): string => {
  if (!value) {
    return "N/A";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "N/A";
  }

  return new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric"
  }).format(parsed);
};

const formatAmountLabel = (amount: number, currency: string | null | undefined): string => {
  const normalizedCurrency = currency?.trim().toUpperCase() || "USD";

  try {
    return new Intl.NumberFormat("en-GB", {
      style: "currency",
      currency: normalizedCurrency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${normalizedCurrency}`;
  }
};

const formatStatusLabel = (status: string): string => {
  const normalized = status.trim();
  if (!normalized) {
    return "Unknown";
  }

  return normalized.replace(/([a-z])([A-Z])/g, "$1 $2");
};

const resolveOrderHeadline = (order: OrderListItemResponse, orderDetail: OrderDetailResponse | undefined) => {
  const firstItem = orderDetail?.items[0];

  return {
    id: order.orderId,
    billerName: firstItem?.billerName?.trim() || "Selected provider",
    serviceName: firstItem?.serviceName?.trim() || "Bill payment",
    amountLabel: formatAmountLabel(order.totalAmountIn, order.originCurrency)
  };
};

const toRecentTransaction = (
  order: OrderListItemResponse,
  orderDetail: OrderDetailResponse | undefined
): DashboardRecentTransaction => {
  const headline = resolveOrderHeadline(order, orderDetail);

  return {
    id: headline.id,
    billerName: headline.billerName,
    serviceName: headline.serviceName,
    dateLabel: formatDateLabel(orderDetail?.submittedAt ?? order.updatedAt ?? order.createdAt),
    amountLabel: headline.amountLabel,
    status: formatStatusLabel(order.status)
  };
};

const toUpcomingBill = (
  order: OrderListItemResponse,
  orderDetail: OrderDetailResponse | undefined
): DashboardUpcomingBill => {
  const headline = resolveOrderHeadline(order, orderDetail);

  return {
    id: headline.id,
    billerName: headline.billerName,
    serviceName: headline.serviceName,
    dueDate: formatDateLabel(order.updatedAt ?? order.createdAt),
    amountLabel: headline.amountLabel
  };
};

const isUpcomingOrder = (status: string) => {
  return !terminalStatuses.has(status.trim().toLowerCase());
};

const getRecentBillPaymentOrders = async (): Promise<OrderListItemResponse[]> => {
  const params = new URLSearchParams({
    pageNumber: "1",
    pageSize: String(dashboardOrderPageSize),
    orderType: billPaymentOrderType
  });

  const response = await apiGet<OrdersPagedResponse>(`/orders?${params.toString()}`);
  return response.items;
};

const getOrderDetailsMap = async (orders: OrderListItemResponse[]): Promise<Map<string, OrderDetailResponse>> => {
  const detailEntries = await Promise.all(
    orders.map(async (order) => {
      try {
        const detail = await apiGet<OrderDetailResponse>(`/orders/${order.orderId}`);
        return [order.orderId, detail] as const;
      } catch {
        return [order.orderId, null] as const;
      }
    })
  );

  const details = detailEntries
    .filter((entry): entry is readonly [string, OrderDetailResponse] => entry[1] !== null)
    .map((entry) => [entry[0], entry[1]] as const);

  return new Map(details);
};

export const getDashboardSummary = async (): Promise<DashboardSummaryResponse> => {
  const orders = await getRecentBillPaymentOrders();

  if (orders.length === 0) {
    return {
      upcomingBills: [],
      recentTransactions: []
    };
  }

  const orderDetails = await getOrderDetailsMap(orders);

  const recentTransactions = orders
    .slice(0, recentTransactionsLimit)
    .map((order) => toRecentTransaction(order, orderDetails.get(order.orderId)));

  const upcomingBills = orders
    .filter((order) => isUpcomingOrder(order.status))
    .slice(0, upcomingBillsLimit)
    .map((order) => toUpcomingBill(order, orderDetails.get(order.orderId)));

  return {
    upcomingBills,
    recentTransactions
  };
};

export const getRecentTransactions = async (): Promise<DashboardRecentTransaction[]> => {
  const summary = await getDashboardSummary();
  return summary.recentTransactions;
};

export const getRecentTransactionById = async (transactionId: string): Promise<DashboardRecentTransaction | null> => {
  const transactions = await getRecentTransactions();
  return transactions.find((item) => item.id === transactionId) ?? null;
};
