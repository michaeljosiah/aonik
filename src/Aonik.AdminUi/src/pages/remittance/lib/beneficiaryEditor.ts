import type { RegisterBeneficiaryRequest } from '@/services/beneficiaryService';

export interface BeneficiaryDraft {
  beneficiaryPartyId: string;
  beneficiaryDisplayName: string;
  beneficiaryPartyType: 'Person' | 'Business';
  destinationType: RegisterBeneficiaryRequest['destinationType'];
  country: string;
  currency: string;
  accountName: string;
  accountNumber: string;
  bankCode: string;
  branchCode: string;
  mobileNetwork: string;
  msisdn: string;
  walletId: string;
}

export const emptyBeneficiaryDraft = (): BeneficiaryDraft => ({
  beneficiaryPartyId: '', beneficiaryDisplayName: '', beneficiaryPartyType: 'Person',
  destinationType: 'Bank', country: '', currency: '', accountName: '', accountNumber: '',
  bankCode: '', branchCode: '', mobileNetwork: '', msisdn: '', walletId: '',
});

/** Changing the rail discards unrelated sensitive inputs instead of carrying them into the request. */
export const changeBeneficiaryRail = (draft: BeneficiaryDraft, destinationType: BeneficiaryDraft['destinationType']): BeneficiaryDraft =>
  ({...draft, destinationType, accountNumber:'', bankCode:'', branchCode:'', mobileNetwork:'', msisdn:'', walletId:''});

export function beneficiaryDraftError(draft: BeneficiaryDraft, customerId: string): string | null {
  if (!customerId) return 'Choose a sending customer.';
  if (!draft.beneficiaryPartyId && !draft.beneficiaryDisplayName.trim()) return 'Enter a recipient name.';
  if (!['Person','Business'].includes(draft.beneficiaryPartyType)) return 'Choose a recipient type.';
  if (!['Bank','MobileMoney','Wallet'].includes(draft.destinationType)) return 'Choose a supported destination type.';
  if (!/^[A-Z]{2}$/.test(draft.country) || !/^[A-Z]{3}$/.test(draft.currency)) return 'Choose a receiving country and currency.';
  if (!draft.accountName.trim() || draft.accountName.trim().length>256 || draft.beneficiaryDisplayName.trim().length>256) return 'Enter names of up to 256 characters.';
  const raw = draft.destinationType==='Bank' ? draft.accountNumber : draft.destinationType==='MobileMoney' ? draft.msisdn : draft.walletId;
  const limit = draft.destinationType==='Bank' ? 64 : draft.destinationType==='MobileMoney' ? 32 : 128;
  if (raw.trim().length<5 || raw.trim().length>limit) return `Enter a destination identifier between 5 and ${limit} characters.`;
  if (draft.destinationType==='Bank' && (!draft.bankCode.trim() || draft.bankCode.trim().length>32 || draft.branchCode.trim().length>32)) return 'Enter a bank code and keep bank/branch codes to 32 characters.';
  if (draft.destinationType==='MobileMoney' && (!draft.mobileNetwork.trim() || draft.mobileNetwork.trim().length>50)) return 'Enter a mobile network of up to 50 characters.';
  return null;
}

/** Raw details exist only for this explicit registration request; no draft persistence is used. */
export function beneficiaryRequest(draft: BeneficiaryDraft, customerId: string): RegisterBeneficiaryRequest {
  const error = beneficiaryDraftError(draft,customerId);
  if (error) throw new Error(error);
  const bank=draft.destinationType==='Bank', mobile=draft.destinationType==='MobileMoney';
  return {
    customerPartyId:customerId, destinationType:draft.destinationType, country:draft.country, currency:draft.currency,
    accountName:draft.accountName.trim(), beneficiaryPartyId:draft.beneficiaryPartyId || null,
    beneficiaryDisplayName:draft.beneficiaryPartyId ? null : draft.beneficiaryDisplayName.trim(), beneficiaryPartyType:draft.beneficiaryPartyType,
    accountNumber:bank ? draft.accountNumber.trim() : null, bankCode:bank ? draft.bankCode.trim() : null,
    branchCode:bank ? draft.branchCode.trim() || null : null, mobileNetwork:mobile ? draft.mobileNetwork.trim() : null,
    msisdn:mobile ? draft.msisdn.trim() : null, walletId:draft.destinationType==='Wallet' ? draft.walletId.trim() : null,
  };
}

export const beneficiaryError = (error: unknown, fallback: string) => error && typeof error==='object' && 'userMessage' in error ? String(error.userMessage || fallback) : error instanceof Error ? error.message : fallback;
