import { apiGet, apiPost } from "./client";
import type { BillPaymentDraftIntent } from "../pages/payments/draftIntent";

type GuestBillPaymentDraftResponse = {
  orderId: string;
  status: string;
  createdAt: string;
};

type GuestBillPaymentDraftDetailResponse = {
  orderId: string;
  status: string;
  createdAt: string;
  countryCode: string;
  currency: string;
  billerId: string;
  billerName: string | null;
  serviceId: string;
  serviceCode: string;
  serviceName: string;
  serviceFieldValues: Record<string, string>;
  isValidated: boolean;
  capturedAt: string;
  validationMode: string | null;
  accountHolderName: string | null;
  requestedAmount: number | null;
  channel: string;
};

export type GuestBillPaymentDraft = {
  orderId: string;
  status: string;
  createdAt: string;
};

export type GuestBillPaymentDraftDetail = GuestBillPaymentDraftDetailResponse;

export const createPublicBillPaymentDraft = async (
  intent: BillPaymentDraftIntent
): Promise<GuestBillPaymentDraft> => {
  const response = await apiPost<GuestBillPaymentDraftResponse>("/public/orders/bill-payments/drafts", intent);

  return {
    orderId: response.orderId,
    status: response.status,
    createdAt: response.createdAt
  };
};

export const getPublicBillPaymentDraft = async (orderId: string): Promise<GuestBillPaymentDraftDetail> => {
  return await apiGet<GuestBillPaymentDraftDetailResponse>(`/public/orders/bill-payments/drafts/${orderId}`);
};
