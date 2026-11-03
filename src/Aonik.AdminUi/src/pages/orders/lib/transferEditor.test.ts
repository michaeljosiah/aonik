import { describe, expect, it } from 'vitest';
import { attemptStorageKey, confirmRequest, destinationMatchesQuote, emptyTransferDraft, loadTransferAttempt, quoteIsCurrent, quoteRequest, transferDraftError } from './transferEditor';
import type { TransferDraft, TransferAttempt } from './transferEditor';
import type { TransferDestination, TransferQuote } from '@/services/remittanceService';

const now = Date.parse('2026-11-01T12:00:00Z');
const draft: TransferDraft = { ...emptyTransferDraft(),customerPartyId:'customer',destinationId:'account',originCountry:'GB',destinationCountry:'NG',originCurrency:'GBP',destinationCurrency:'NGN',amount:'125.50',purposeCode:'FamilySupport' };
const quote: TransferQuote = { pricingQuoteId:'quote',quoteType:'Remittance',originCountry:'GB',destinationCountry:'NG',originCurrency:'GBP',destinationCurrency:'NGN',originAmount:125.50,destinationAmount:250000,feesTotal:2,totalAmount:127.50,exchangeRate:1992.03,rateMarkup:0,pricingPolicyId:'policy',pricingPolicyVersion:'v1',fxRateId:'fx',rateTimestamp:new Date(now).toISOString(),expiresAt:new Date(now+60000).toISOString(),feeBreakdown:[],supportedDestinationMethods:[{destinationType:'Bank',countryCode:'NG',currency:'NGN'}] };
const destination: TransferDestination = { externalPayoutAccountId:'account',customerPartyId:'customer',beneficiaryPartyId:'recipient',beneficiaryName:'Recipient',destinationType:'Bank',maskedAccountIdentifier:'••1234',currency:'NGN',accountName:'Recipient',providerCode:'Configured provider',isVerified:true };

describe('transfer editor financial guards', () => {
  it.each(['','0','-1','1e3','0x10','1.23456','1234567890123456','NaN','Infinity'])('refuses unsafe amount text %s before JSON numeric conversion', amount => {
    expect(transferDraftError({...draft,amount})).not.toBeNull();
    expect(()=>quoteRequest({...draft,amount})).toThrow();
  });
  it('quotes exactly one amount basis and preserves country, currency and customer references', () => {
    expect(quoteRequest(draft)).toEqual({customerPartyId:'customer',originCountry:'GB',destinationCountry:'NG',originCurrency:'GBP',destinationCurrency:'NGN',originAmount:125.5,purposeCode:'FamilySupport'});
    const receive=quoteRequest({...draft,amountSide:'receive'});
    expect(receive.destinationAmount).toBe(125.5);expect(receive).not.toHaveProperty('originAmount');
  });
  it('refuses expiry at the exact boundary and invalid timestamps', () => {
    expect(quoteIsCurrent(quote,now+59999)).toBe(true);
    expect(quoteIsCurrent(quote,now+60000)).toBe(false);
    expect(quoteIsCurrent({...quote,expiresAt:'invalid'},now)).toBe(false);
    expect(()=>confirmRequest(draft,quote,destination,now+60000)).toThrow('expired');
  });
  it.each([
    {...destination,isVerified:false}, {...destination,customerPartyId:'another'},
    {...destination,currency:'USD'}, {...destination,destinationType:'Wallet'},
  ])('refuses an unverified, foreign or unsupported destination', value => {
    expect(destinationMatchesQuote(value,quote,draft.customerPartyId)).toBe(false);
    expect(()=>confirmRequest(draft,quote,value,now)).toThrow();
  });
  it('binds the chosen rail, quote, customer, purpose and provider without raw account details', () => {
    const request=confirmRequest({...draft,narration:'  Monthly support  '},quote,destination,now);
    expect(request).toEqual({pricingQuoteId:'quote',customerPartyId:'customer',destinationExternalAccountId:'account',purposeCode:'FamilySupport',narration:'Monthly support',providerCode:'Configured provider'});
    expect(JSON.stringify(request)).not.toContain('1234');
    expect(()=>confirmRequest({...draft,destinationCountry:'GH'},quote,destination,now)).toThrow('changed');
  });
  it('recovers the original key and immutable request without requiring an unexpired quote', () => {
    const attempt: TransferAttempt={key:'stable-key',request:confirmRequest(draft,quote,destination,now),quote:{...quote,expiresAt:new Date(now-60000).toISOString()},maskedDestination:'Bank · ••1234'};
    expect(loadTransferAttempt({getItem:()=>JSON.stringify(attempt)},'scope')).toEqual(attempt);
    expect(loadTransferAttempt({getItem:()=>null},'scope')).toBeNull();
    expect(attemptStorageKey('tenant-a','user')).not.toBe(attemptStorageKey('tenant-b','user'));
    expect(attemptStorageKey('tenant','user-a')).not.toBe(attemptStorageKey('tenant','user-b'));
  });
  it('fails closed on a corrupted pending confirmation', () => {
    expect(()=>loadTransferAttempt({getItem:()=>'{bad'},'scope')).toThrow();
    expect(()=>loadTransferAttempt({getItem:()=>JSON.stringify({key:'lost-request'})},'scope')).toThrow('previous confirmation');
  });
});
