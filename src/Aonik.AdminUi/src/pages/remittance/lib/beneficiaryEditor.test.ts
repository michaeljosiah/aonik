import { describe, expect, it } from 'vitest';
import { beneficiaryDraftError, beneficiaryRequest, changeBeneficiaryRail, emptyBeneficiaryDraft } from './beneficiaryEditor';

const bank={...emptyBeneficiaryDraft(),beneficiaryDisplayName:'Tayo Example',accountName:'Tayo Example',country:'NG',currency:'NGN',bankCode:'044',accountNumber:'0690000040'};
describe('beneficiary registration editor',()=>{
  it('requires a customer and valid identity/corridor before submitting',()=>{
    expect(beneficiaryDraftError(bank,'')).toContain('customer');
    expect(()=>beneficiaryRequest({...bank,beneficiaryDisplayName:''},'customer')).toThrow('recipient name');
    expect(()=>beneficiaryRequest({...bank,country:'Nigeria'},'customer')).toThrow('country');
    expect(()=>beneficiaryRequest({...bank,accountName:'x'.repeat(257)},'customer')).toThrow('256');
  });
  it.each(['','1234','1'.repeat(65)])('rejects incomplete/oversized bank identifiers %s',accountNumber=>{
    expect(()=>beneficiaryRequest({...bank,accountNumber},'customer')).toThrow('identifier');
  });
  it('sends only the chosen rail and does not persist raw draft data',()=>{
    const request=beneficiaryRequest({...bank,msisdn:'stale-phone',walletId:'stale-wallet'},'customer');
    expect(request.accountNumber).toBe('0690000040');expect(request.msisdn).toBeNull();expect(request.walletId).toBeNull();
    const mobile=changeBeneficiaryRail(bank,'MobileMoney');
    expect(mobile.accountNumber).toBe('');expect(mobile.bankCode).toBe('');
    expect(()=>beneficiaryRequest(mobile,'customer')).toThrow();
    expect(beneficiaryRequest({...mobile,mobileNetwork:'MTN',msisdn:'+2348000000040'},'customer')).toMatchObject({accountNumber:null,bankCode:null,walletId:null,msisdn:'+2348000000040'});
    expect(beneficiaryRequest({...changeBeneficiaryRail(bank,'Wallet'),walletId:'wallet-0040'},'customer')).toMatchObject({accountNumber:null,msisdn:null,walletId:'wallet-0040'});
    expect(emptyBeneficiaryDraft().accountNumber).toBe('');
  });
  it('adds another destination to the selected existing recipient',()=>{
    expect(beneficiaryRequest({...bank,beneficiaryPartyId:'recipient',beneficiaryDisplayName:''},'customer')).toMatchObject({customerPartyId:'customer',beneficiaryPartyId:'recipient',beneficiaryDisplayName:null});
  });
});
