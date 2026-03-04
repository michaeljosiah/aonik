export const draftIntentStorageKey = "payabo.billPaymentDraftIntent";
export const draftOrderIdStorageKey = "payabo.billPaymentDraftOrderId";

export type BillPaymentDraftIntent = {
  billerId: string;
  serviceId: string;
  billerName: string | null;
  serviceCode: string;
  serviceName: string;
  countryCode: string;
  currency: string;
  serviceFieldValues: Record<string, string>;
  isValidated: boolean;
  capturedAt: string;
  validationMode: string | null;
  accountHolderName: string | null;
  requestedAmount: number | null;
  channel: string;
};
