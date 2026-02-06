import { apiPost } from "./client";

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
