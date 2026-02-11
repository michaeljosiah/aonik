export type CheckoutAttemptState = {
  orderId: string;
  paymentIntentId: string;
  providerReference: string;
  createdAt: string;
};

const checkoutAttemptStorageKey = "payabo.checkout-attempt";

export const saveCheckoutAttemptState = (state: CheckoutAttemptState) => {
  try {
    sessionStorage.setItem(checkoutAttemptStorageKey, JSON.stringify(state));
  } catch {
    // ignore storage errors
  }
};

export const readCheckoutAttemptState = (): CheckoutAttemptState | null => {
  try {
    const raw = sessionStorage.getItem(checkoutAttemptStorageKey);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as Partial<CheckoutAttemptState>;
    if (!parsed.orderId || !parsed.paymentIntentId || !parsed.providerReference || !parsed.createdAt) {
      return null;
    }

    return {
      orderId: parsed.orderId,
      paymentIntentId: parsed.paymentIntentId,
      providerReference: parsed.providerReference,
      createdAt: parsed.createdAt
    };
  } catch {
    return null;
  }
};
