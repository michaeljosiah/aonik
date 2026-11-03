import { api } from '@/lib/api';
import type { TransferApiContext, TransferDestination } from './remittanceService';

export interface BeneficiaryDestination extends TransferDestination {
  bankCode: string | null;
  mobileNetwork: string | null;
  relationshipTypeCode: string;
}

export interface ManagedBeneficiary { destination: BeneficiaryDestination; notes: string | null }
export interface RegisterBeneficiaryRequest {
  customerPartyId: string;
  destinationType: 'Bank' | 'MobileMoney' | 'Wallet';
  country: string;
  currency: string;
  accountName: string;
  beneficiaryPartyId: string | null;
  beneficiaryDisplayName: string | null;
  beneficiaryPartyType: 'Person' | 'Business';
  accountNumber: string | null;
  bankCode: string | null;
  branchCode: string | null;
  mobileNetwork: string | null;
  msisdn: string | null;
  walletId: string | null;
}

const customerPath = (id: string) => `/remittance/customers/${encodeURIComponent(id)}`;
export const beneficiaryService = {
  list: (customerId: string, context: TransferApiContext) => api.get<{customerPartyId: string; beneficiaries: ManagedBeneficiary[]}>(`${customerPath(customerId)}/beneficiaries`, context),
  register: (beneficiary: RegisterBeneficiaryRequest, context: TransferApiContext) => api.post<BeneficiaryDestination>('/remittance/beneficiaries/verify', {beneficiary}, context),
  updateNotes: (customerId: string, recipientId: string, notes: string, context: TransferApiContext) => api.patch<void>(`${customerPath(customerId)}/beneficiaries/${encodeURIComponent(recipientId)}/notes`, {notes}, context),
  remove: (customerId: string, destinationId: string, context: TransferApiContext) => api.delete<void>(`${customerPath(customerId)}/destinations/${encodeURIComponent(destinationId)}`, context),
};
