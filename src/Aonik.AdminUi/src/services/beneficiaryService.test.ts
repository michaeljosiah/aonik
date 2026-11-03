import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { beneficiaryRequest, emptyBeneficiaryDraft } from '@/pages/remittance/lib/beneficiaryEditor';
import { beneficiaryService } from './beneficiaryService';

vi.mock('@/lib/api',()=>({api:{get:vi.fn(),post:vi.fn(),patch:vi.fn(),delete:vi.fn()}}));
const context={headers:{'X-Tenant-Id':'original-tenant'}};
describe('operator beneficiary transport',()=>{
  beforeEach(()=>vi.clearAllMocks());
  it('pins customer/tenant context for reads, note edits and individual removal',async()=>{
    await beneficiaryService.list('customer',context);
    await beneficiaryService.updateNotes('customer','recipient','Support',context);
    await beneficiaryService.remove('customer','destination',context);
    expect(api.get).toHaveBeenCalledWith('/remittance/customers/customer/beneficiaries',context);
    expect(api.patch).toHaveBeenCalledWith('/remittance/customers/customer/beneficiaries/recipient/notes',{notes:'Support'},context);
    expect(api.delete).toHaveBeenCalledWith('/remittance/customers/customer/destinations/destination',context);
  });
  it('registers only through the staff verification endpoint without automatic retry',async()=>{
    const beneficiary=beneficiaryRequest({...emptyBeneficiaryDraft(),beneficiaryDisplayName:'Example',accountName:'Example',destinationType:'Wallet',country:'NG',currency:'NGN',walletId:'wallet-0040'},'customer');
    vi.mocked(api.post).mockRejectedValueOnce(new Error('Unavailable'));
    await expect(beneficiaryService.register(beneficiary,context)).rejects.toThrow('Unavailable');
    expect(api.post).toHaveBeenCalledExactlyOnceWith('/remittance/beneficiaries/verify',{beneficiary},context);
  });
});
