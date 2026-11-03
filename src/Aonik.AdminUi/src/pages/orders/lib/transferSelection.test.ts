import { describe, expect, it } from 'vitest';
import { readTransferSelection, selectedTransferDestination, transferEditorPath } from './transferSelection';
import type { TransferDestination } from '@/services/remittanceService';

const customerId='22222222-2222-4222-8222-222222222222';
const destinationId='33333333-3333-4333-8333-333333333333';
const destination: TransferDestination={externalPayoutAccountId:destinationId,customerPartyId:customerId,beneficiaryPartyId:'recipient',beneficiaryName:'Recipient',destinationType:'Bank',maskedAccountIdentifier:'•••• 4821',currency:'NGN',accountName:'Recipient',isVerified:true};

describe('beneficiary transfer selection',()=>{
  it('passes only customer and destination references in a reloadable editor link',()=>{
    const url=transferEditorPath(customerId,destinationId);
    expect(url.split('?')[0]).toBe('/orders/transfers/new');
    expect(readTransferSelection(url.slice(url.indexOf('?')))).toEqual({customerId,destinationId});
    expect(url).not.toContain('Recipient');expect(url).not.toContain('4821');
    expect(readTransferSelection('')).toBeNull();
    expect(readTransferSelection('?unrelated=value')).toBeNull();
  });
  it.each(['?customerPartyId=bad&destinationId=bad',`?customerPartyId=${customerId}`,`?destinationId=${destinationId}`])('rejects invalid or incomplete reference pairs %s',search=>{
    expect(()=>readTransferSelection(search)).toThrow('incomplete or invalid');
  });
  it('resolves a verified destination only from the current API result for this customer',()=>{
    expect(selectedTransferDestination([destination],customerId,destinationId)).toBe(destination);
    expect(()=>selectedTransferDestination([],customerId,destinationId)).toThrow('unavailable');
    expect(()=>selectedTransferDestination([{...destination,isVerified:false}],customerId,destinationId)).toThrow('verification');
    expect(()=>selectedTransferDestination([{...destination,customerPartyId:'another-customer'}],customerId,destinationId)).toThrow('unavailable');
    expect(()=>selectedTransferDestination([{...destination,externalPayoutAccountId:'another-destination'}],customerId,destinationId)).toThrow('unavailable');
  });
});
