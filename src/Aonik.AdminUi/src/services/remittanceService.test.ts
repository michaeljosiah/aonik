import { beforeEach, describe, expect, it, vi } from 'vitest';
import apiClient, { api } from '@/lib/api';
import { remittanceService, transferWasNotStarted } from './remittanceService';
import type { ConfirmTransferRequest } from './remittanceService';

vi.mock('@/lib/api',()=>({default:{get:vi.fn()},api:{get:vi.fn(),post:vi.fn()}}));
const transfer: ConfirmTransferRequest={pricingQuoteId:'quote',customerPartyId:'customer',destinationExternalAccountId:'saved-destination',purposeCode:'FamilySupport',narration:null,providerCode:null};
const context={headers:{'X-Tenant-Id':'tenant-at-click'},signal:new AbortController().signal};

describe('operator transfer transport',()=>{
  beforeEach(()=>vi.clearAllMocks());
  it('abandons an uncertain request only with the explicit server verdict',()=>{
    expect(transferWasNotStarted({response:{status:422,headers:{'x-transfer-execution':'not-started'}}})).toBe(true);
    for(const error of [null,new Error('Network timeout'),{response:{status:422}},{response:{status:500,headers:{}}}]) expect(transferWasNotStarted(error)).toBe(false);
  });
  it('captures tenant context, explicit confirmation and the same idempotency key across a failed response',async()=>{
    vi.mocked(api.post).mockRejectedValueOnce(new Error('Lost response')).mockResolvedValueOnce({orderId:'original'});
    await expect(remittanceService.confirm(transfer,'stable-key',context)).rejects.toThrow('Lost response');
    expect(api.post).toHaveBeenCalledTimes(1);
    await remittanceService.confirm(transfer,'stable-key',context);
    for(const call of vi.mocked(api.post).mock.calls) expect(call).toEqual(['/remittance/confirm',{transfer,confirmExecution:true},{...context,headers:{...context.headers,'Idempotency-Key':'stable-key'}}]);
  });
  it('uses operator read endpoints with cancellation and the captured tenant',async()=>{
    await remittanceService.destinations('customer',context);await remittanceService.get('order',context);
    expect(api.get).toHaveBeenCalledWith('/remittance/customers/customer/destinations',context);
    expect(api.get).toHaveBeenCalledWith('/remittance/order',context);
  });
  it('downloads the canonical server PDF as a blob with the captured tenant',async()=>{
    const pdf=new Blob(['%PDF-'],{type:'application/pdf'});
    vi.mocked(apiClient.get).mockResolvedValue({data:pdf});
    expect(await remittanceService.receipt('order',context)).toBe(pdf);
    expect(apiClient.get).toHaveBeenCalledWith('/remittance/order/receipt',{...context,responseType:'blob'});
  });
});
