import type { ConfirmTransferRequest, TransferDestination, TransferQuote, TransferQuoteRequest } from '@/services/remittanceService';

export interface TransferDraft {
  customerPartyId: string;
  destinationId: string;
  originCountry: string;
  destinationCountry: string;
  originCurrency: string;
  destinationCurrency: string;
  amount: string;
  amountSide: 'send' | 'receive';
  purposeCode: string;
  narration: string;
}

export const emptyTransferDraft = (): TransferDraft => ({ customerPartyId:'', destinationId:'', originCountry:'', destinationCountry:'', originCurrency:'', destinationCurrency:'', amount:'', amountSide:'send', purposeCode:'', narration:'' });

/** Validate text before converting to the API's decimal JSON number. */
export function transferDraftError(draft: TransferDraft): string | null {
  if (!draft.customerPartyId) return 'Choose a customer.';
  if (!draft.destinationId) return 'Choose a saved destination.';
  if (![draft.originCountry,draft.destinationCountry].every(value => /^[A-Z]{2}$/.test(value))) return 'Choose the sending and receiving countries.';
  if (![draft.originCurrency,draft.destinationCurrency].every(value => /^[A-Z]{3}$/.test(value))) return 'Choose the sending and receiving currencies.';
  if (!/^\d+(\.\d{1,4})?$/.test(draft.amount) || Number(draft.amount) <= 0) return 'Enter a positive amount with at most four decimal places.';
  if (draft.amount.replace('.','').replace(/^0+/,'').length > 15) return 'Use at most 15 significant digits for the amount.';
  if (!draft.purposeCode.trim() || draft.purposeCode.trim().length > 64) return 'Enter a purpose code of up to 64 characters.';
  if (draft.narration.trim().length > 256) return 'Keep the transfer note to 256 characters.';
  return null;
}

export function quoteRequest(draft: TransferDraft): TransferQuoteRequest {
  const error = transferDraftError(draft);
  if (error) throw new Error(error);
  return { customerPartyId:draft.customerPartyId, originCountry:draft.originCountry, destinationCountry:draft.destinationCountry, originCurrency:draft.originCurrency, destinationCurrency:draft.destinationCurrency, ...(draft.amountSide==='send' ? {originAmount:Number(draft.amount)} : {destinationAmount:Number(draft.amount)}), purposeCode:draft.purposeCode.trim() };
}

export function quoteIsCurrent(quote: TransferQuote | null, now: number): boolean {
  return !!quote && Number.isFinite(Date.parse(quote.expiresAt)) && Date.parse(quote.expiresAt) > now;
}

export function destinationMatchesQuote(destination: TransferDestination, quote: TransferQuote, customerId: string): boolean {
  return destination.customerPartyId === customerId && destination.isVerified && destination.currency.toUpperCase() === quote.destinationCurrency &&
    quote.supportedDestinationMethods.some(method => method.destinationType.toLowerCase() === destination.destinationType.toLowerCase() && method.countryCode === quote.destinationCountry && method.currency === quote.destinationCurrency && (!method.network || method.network === destination.mobileNetwork));
}

export function confirmRequest(draft: TransferDraft, quote: TransferQuote, destination: TransferDestination, now: number): ConfirmTransferRequest {
  const error = transferDraftError(draft);
  if (error) throw new Error(error);
  if (!quoteIsCurrent(quote,now)) throw new Error('This quote has expired. Get a new quote.');
  if (!destinationMatchesQuote(destination,quote,draft.customerPartyId)) throw new Error('Choose a verified destination supported by this quote.');
  if (destination.externalPayoutAccountId !== draft.destinationId || quote.originCountry !== draft.originCountry || quote.destinationCountry !== draft.destinationCountry || quote.originCurrency !== draft.originCurrency || quote.destinationCurrency !== draft.destinationCurrency) throw new Error('The transfer details changed. Get a new quote.');
  return { pricingQuoteId:quote.pricingQuoteId, customerPartyId:draft.customerPartyId, destinationExternalAccountId:draft.destinationId, purposeCode:draft.purposeCode.trim(), narration:draft.narration.trim() || null, providerCode:destination.providerCode || null };
}

export interface TransferAttempt { key: string; request: ConfirmTransferRequest; quote: TransferQuote; maskedDestination: string }
export const attemptStorageKey = (tenantId: string, userId: string) => `aonik:transfer-confirm:${encodeURIComponent(tenantId)}:${encodeURIComponent(userId)}`;

export function loadTransferAttempt(storage: Pick<Storage,'getItem'>, key: string): TransferAttempt | null {
  const raw = storage.getItem(key);
  if (!raw) return null;
  const value = JSON.parse(raw) as TransferAttempt;
  if (!value?.key || !value.request?.pricingQuoteId || !value.request?.customerPartyId || !value.request?.destinationExternalAccountId || !value.quote?.pricingQuoteId || value.quote.pricingQuoteId !== value.request.pricingQuoteId) throw new Error('A previous confirmation could not be recovered. Check transfer activity before starting another transfer.');
  return value;
}
