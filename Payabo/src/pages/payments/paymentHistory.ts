export type PaymentHistoryItem = {
  id: string;
  userId: string;
  orderId: string;
  paymentIntentId: string;
  providerReference: string;
  status: string;
  orderStatus: string;
  amount: number | null;
  currency: string;
  serviceName: string;
  billerName: string | null;
  createdAt: string;
  updatedAt: string;
};

const storageKey = "payabo.payment-history";

const readAll = (): PaymentHistoryItem[] => {
  try {
    const raw = localStorage.getItem(storageKey);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw) as PaymentHistoryItem[];
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed;
  } catch {
    return [];
  }
};

const writeAll = (items: PaymentHistoryItem[]) => {
  try {
    localStorage.setItem(storageKey, JSON.stringify(items));
  } catch {
    // ignore storage errors
  }
};

export const upsertPaymentHistory = (item: Omit<PaymentHistoryItem, "id" | "updatedAt">) => {
  const items = readAll();
  const index = items.findIndex((existing) => existing.userId === item.userId && existing.orderId === item.orderId);

  const now = new Date().toISOString();
  if (index >= 0) {
    items[index] = {
      ...items[index],
      ...item,
      updatedAt: now
    };
  } else {
    items.unshift({
      ...item,
      id: crypto.randomUUID(),
      updatedAt: now
    });
  }

  writeAll(items);
};

export const listPaymentHistoryForUser = (userId: string): PaymentHistoryItem[] => {
  return readAll()
    .filter((item) => item.userId === userId)
    .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());
};

export const getPaymentHistoryItemForUser = (userId: string, id: string): PaymentHistoryItem | null => {
  return readAll().find((item) => item.userId === userId && item.id === id) ?? null;
};
