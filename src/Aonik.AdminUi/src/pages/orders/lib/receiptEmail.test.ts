import { describe, expect, it } from 'vitest';
import { readPendingReceiptEmail, receiptEmailHash, receiptEmailStorageKey } from './receiptEmail';

describe('receipt email recovery',()=>{
  it('scopes references to the authenticated user, tenant and order',()=>{
    const key=receiptEmailStorageKey('tenant','user','order');
    for(const scope of [['other','user','order'],['tenant','other','order'],['tenant','user','other']])
      expect(receiptEmailStorageKey(...scope as [string,string,string])).not.toBe(key);
  });

  it('normalizes an address to a hash without retaining the address',async()=>{
    const hash=await receiptEmailHash(' Nadia@Example.test ');
    expect(hash).toBe(await receiptEmailHash('nadia@example.test'));
    expect(hash).toMatch(/^[a-f0-9]{64}$/);
    const pending={requestId:'request',contactId:'contact',emailHash:hash};
    expect(readPendingReceiptEmail({getItem:()=>JSON.stringify(pending)},'scope')).toEqual(pending);
    expect(JSON.stringify(pending)).not.toContain('nadia');
  });

  it('blocks recovery of a malformed previous request',()=>{
    expect(readPendingReceiptEmail({getItem:()=>null},'scope')).toBeNull();
    for(const raw of ['{','{}','{"requestId":"id","contactId":"contact","emailHash":"raw-email"}'])
      expect(()=>readPendingReceiptEmail({getItem:()=>raw},'scope')).toThrow();
  });
});
