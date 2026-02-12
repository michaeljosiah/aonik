import { apiGet } from "./client";

export type PaymentInstrument = {
  id: string;
  brand: string;
  type: "credit" | "debit";
  last4: string;
  expiryMonth: number;
  expiryYear: number;
};

const storageKey = "payabo.payment-instruments";

const readLocalInstruments = (userId: string): PaymentInstrument[] => {
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw) as Record<string, PaymentInstrument[]>;
    const instruments = parsed[userId];
    if (!Array.isArray(instruments)) {
      return [];
    }

    return instruments;
  } catch {
    return [];
  }
};

const seedLocalInstruments = (userId: string): PaymentInstrument[] => {
  const seeded: PaymentInstrument[] = [
    {
      id: "pi_card_visa_1",
      brand: "Visa",
      type: "debit",
      last4: "7568",
      expiryMonth: 12,
      expiryYear: 2028
    },
    {
      id: "pi_card_mc_1",
      brand: "Mastercard",
      type: "credit",
      last4: "1982",
      expiryMonth: 5,
      expiryYear: 2027
    }
  ];

  try {
    const raw = window.localStorage.getItem(storageKey);
    const parsed = raw ? (JSON.parse(raw) as Record<string, PaymentInstrument[]>) : {};
    parsed[userId] = seeded;
    window.localStorage.setItem(storageKey, JSON.stringify(parsed));
  } catch {
    // ignore storage errors
  }

  return seeded;
};

export const getPaymentInstrumentsForUser = async (userId: string): Promise<PaymentInstrument[]> => {
  try {
    return await apiGet<PaymentInstrument[]>(`/public/payments/instruments?userId=${encodeURIComponent(userId)}`);
  } catch {
    const local = readLocalInstruments(userId);
    if (local.length > 0) {
      return local;
    }

    return seedLocalInstruments(userId);
  }
};
