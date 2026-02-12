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


export const resolvePaymentIntentIdForReturn = (options: {
  paymentIntentIdFromQuery: string | null;
  providerReferenceFromQuery: string | null;
  savedAttempt: CheckoutAttemptState | null;
}): string => {
  const paymentIntentIdFromQuery = options.paymentIntentIdFromQuery?.trim() ?? "";
  if (paymentIntentIdFromQuery) {
    return paymentIntentIdFromQuery;
  }

  const providerReferenceFromQuery = options.providerReferenceFromQuery?.trim() ?? "";
  const savedAttempt = options.savedAttempt;

  if (!savedAttempt) {
    return "";
  }

  if (!providerReferenceFromQuery) {
    return savedAttempt.paymentIntentId;
  }

  return providerReferenceFromQuery === savedAttempt.providerReference ? savedAttempt.paymentIntentId : "";
};
