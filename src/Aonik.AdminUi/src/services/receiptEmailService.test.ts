import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { receiptEmailService } from './receiptEmailService';

vi.mock('@/lib/api',()=>({api:{get:vi.fn(),post:vi.fn(),put:vi.fn()}}));

describe('receipt email transport',()=>{
  const context={headers:{'X-Tenant-Id':'tenant-at-confirmation'}};
  const request={requestId:'stable-request',contactId:'saved-contact',expectedEmail:'nadia@example.test',confirmSend:true as const};
  beforeEach(()=>vi.clearAllMocks());

  it('retains the same request and captured tenant after a lost response, without an automatic retry',async()=>{
    vi.mocked(api.post).mockRejectedValueOnce(new Error('Lost response')).mockResolvedValueOnce({status:'Accepted'});
    await expect(receiptEmailService.send('order',request,context)).rejects.toThrow('Lost response');
    expect(api.post).toHaveBeenCalledTimes(1);
    await receiptEmailService.send('order',request,context);
    for(const call of vi.mocked(api.post).mock.calls) expect(call).toEqual(['/remittance/order/receipt/email',request,context]);
  });

  it('loads recipients and checks status with read requests only',async()=>{
    await receiptEmailService.options('order',context);
    await receiptEmailService.status('order','stable-request',context);
    expect(api.get).toHaveBeenCalledWith('/remittance/order/receipt/email',context);
    expect(api.get).toHaveBeenCalledWith('/remittance/order/receipt/email/stable-request',context);
    expect(api.post).not.toHaveBeenCalled();
  });
  it('reads bounded history pages with captured tenant and cancellation',async()=>{
    const captured={...context,signal:new AbortController().signal};
    await receiptEmailService.history('order',2,captured);
    expect(api.get).toHaveBeenCalledWith('/remittance/order/receipt/email/history',{...captured,params:{pageNumber:2,pageSize:20}});
    expect(api.post).not.toHaveBeenCalled();
  });
  it('reviews and saves the original bounced contact without dispatching a receipt or changing send references',async()=>{
    await receiptEmailService.reviewRecipient('order','bounced-request',context);
    const update={expectedEmail:'old@example.test',newEmail:'corrected@example.test',confirmUpdate:true as const};
    await receiptEmailService.updateRecipient('order','bounced-request',update,context);
    expect(api.get).toHaveBeenCalledWith('/remittance/order/receipt/email/bounced-request/recipient',context);
    expect(api.put).toHaveBeenCalledWith('/remittance/order/receipt/email/bounced-request/recipient',update,context);
    expect(api.post).not.toHaveBeenCalled();
  });
});
