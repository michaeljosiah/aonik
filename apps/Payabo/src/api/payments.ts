import { apiGet, apiPost } from "./client";

type PublicPaymentIntentResponse = {
  paymentIntentId: string;
  orderId: string;
  amount: number;
  currency: string;
  status: string;
  provider: string;
  providerReference: string;
  clientSecret: string | null;
  checkoutUrl: string | null;
  createdAt: string;
};

export type PublicPaymentIntent = PublicPaymentIntentResponse;

export const createPublicPaymentIntent = async (request: {
  orderId: string;
  provider: string;
  paymentMethodType: string;
  returnUrl?: string;
  cancelUrl?: string;
}): Promise<PublicPaymentIntent> => {
  return await apiPost<PublicPaymentIntentResponse>("/public/payments/intents", request);
};


export type PublicPaymentIntentStatus = {
  paymentIntentId: string;
  orderId: string;
  amount: number;
  currency: string;
  status: string;
  providerReference: string;
  createdAt: string;
  orderStatus: string;
};

export const getPublicPaymentIntentStatus = async (request: {
  orderId: string;
  paymentIntentId?: string;
  providerReference?: string;
}): Promise<PublicPaymentIntentStatus> => {
  const params = new URLSearchParams({ orderId: request.orderId });

  if (request.paymentIntentId) {
    params.set("paymentIntentId", request.paymentIntentId);
  }

  if (request.providerReference) {
    params.set("providerReference", request.providerReference);
  }

  return await apiGet<PublicPaymentIntentStatus>(`/public/payments/intents/status?${params.toString()}`);
};
