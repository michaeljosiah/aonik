import { describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { customerService } from './customerService';

vi.mock('@/lib/api',()=>({default:{},api:{get:vi.fn()}}));

describe('customer lookup for transfer selection',()=>{
  it('pins the tenant and cancellation context supplied by the editor',async()=>{
    const context={headers:{'X-Tenant-Id':'tenant-at-navigation'},signal:new AbortController().signal};
    await customerService.get('customer',context);
    expect(api.get).toHaveBeenCalledWith('/admin/customers/customer',context);
  });
});
