import apiClient, { api } from '@/lib/api';
import type { FeeBreakdownItem } from '@/types';

export interface TransferQuoteRequest {
  customerPartyId: string;
  originCountry: string;
  destinationCountry: string;
  originCurrency: string;
  destinationCurrency: string;
  originAmount?: number;
  destinationAmount?: number;
  purposeCode?: string;
}

export interface TransferQuote {
  pricingQuoteId: string;
  quoteType: string;
  originCountry: string;
  destinationCountry: string;
  originCurrency: string;
  destinationCurrency: string;
  originAmount: number;
  destinationAmount: number;
  feesTotal: number;
  totalAmount: number;
  exchangeRate: number;
  rateMarkup: number;
  pricingPolicyId: string;
  pricingPolicyVersion: string;
  fxRateId: string;
  rateTimestamp: string;
  expiresAt: string;
  feeBreakdown: FeeBreakdownItem[];
  supportedDestinationMethods: { destinationType: string; countryCode: string; currency: string; network?: string | null }[];
}

export interface TransferDestination {
  externalPayoutAccountId: string;
  customerPartyId: string;
  beneficiaryPartyId: string;
  beneficiaryName: string;
  destinationType: string;
  maskedAccountIdentifier: string;
  currency: string;
  accountName: string;
  providerCode?: string | null;
  mobileNetwork?: string | null;
  isVerified: boolean;
}

export interface ConfirmTransferRequest {
  pricingQuoteId: string;
  customerPartyId: string;
  destinationExternalAccountId: string;
  purposeCode: string;
  narration: string | null;
  providerCode: string | null;
}

export interface TransferOrder {
  orderId: string;
  orderNumber: string;
  status: string;
  customerPartyId: string;
  beneficiaryPartyId?: string | null;
  destinationExternalAccountId: string;
  destinationType: string;
  maskedAccountIdentifier: string;
  originCountry: string;
  destinationCountry: string;
  originCurrency: string;
  destinationCurrency: string;
  originAmount: number;
  destinationAmount: number;
  feesTotal: number;
  totalAmount: number;
  exchangeRate: number;
  pricingQuoteId: string;
  payoutId?: string | null;
  providerCode?: string | null;
  clientReference?: string | null;
  providerReference?: string | null;
  transmissionStatus?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  settledAt?: string | null;
}

export interface TransferApiContext { headers: { 'X-Tenant-Id': string }; signal?: AbortSignal }

/** Only the server's explicit no-order verdict makes an execution retry safe to abandon. */
export function transferWasNotStarted(error: unknown): boolean {
  const response = (error as { response?: { headers?: Record<string,unknown> } } | null)?.response;
  return response?.headers?.['x-transfer-execution'] === 'not-started';
}

export const remittanceService = {
  receipt: (id: string, context: TransferApiContext) => apiClient.get<Blob>(`/remittance/${encodeURIComponent(id)}/receipt`,{...context,responseType:'blob'}).then(response=>response.data),
  quote: (request: TransferQuoteRequest, context: TransferApiContext) => api.post<TransferQuote>('/remittance/quote', request, context),
  destinations: (customerId: string, context: TransferApiContext) => api.get<{ customerPartyId: string; beneficiaries: TransferDestination[] }>(`/remittance/customers/${encodeURIComponent(customerId)}/destinations`, context),
  confirm: (transfer: ConfirmTransferRequest, idempotencyKey: string, context: TransferApiContext) => api.post<TransferOrder>('/remittance/confirm', { transfer, confirmExecution: true }, { ...context, headers: { ...context.headers, 'Idempotency-Key': idempotencyKey } }),
  get: (id: string, context: TransferApiContext) => api.get<TransferOrder>(`/remittance/${encodeURIComponent(id)}`, context),
};
