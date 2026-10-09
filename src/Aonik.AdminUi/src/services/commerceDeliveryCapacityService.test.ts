import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { commerceDeliveryCapacityService } from './commerceDeliveryCapacityService';

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), put: vi.fn() } }));

describe('delivery capacity transport', () => {
  beforeEach(() => vi.clearAllMocks());

  it('binds bounded reads to the captured tenant and cancellation signal', async () => {
    const config = { headers: { 'X-Tenant-Id': 'original-tenant' }, signal: new AbortController().signal };
    await commerceDeliveryCapacityService.list('2026-10-01', 31, config);
    await commerceDeliveryCapacityService.availability('2026-10-01', 31, config);
    expect(api.get).toHaveBeenNthCalledWith(1, '/commerce/admin/delivery-capacity?fromDate=2026-10-01&days=31', config);
    expect(api.get).toHaveBeenNthCalledWith(2, '/commerce/config/delivery/dates?fromDate=2026-10-01&days=31', config);
  });

  it('preserves explicit zero and the original tenant/version on writes', async () => {
    const config = { headers: { 'X-Tenant-Id': 'original-tenant' }, signal: new AbortController().signal };
    const request = { unit: 'box' as const, capacity: 0, expectedVersion: 'observed-version' };
    await commerceDeliveryCapacityService.update('2026-10-15', request, config);
    expect(api.put).toHaveBeenCalledWith('/commerce/admin/delivery-capacity/2026-10-15', request, config);
  });

  it('passes concurrency failures back without silently refreshing and retrying the write', async () => {
    const conflict = { response: { status: 409 } };
    vi.mocked(api.put).mockRejectedValue(conflict);
    await expect(commerceDeliveryCapacityService.update('2026-10-15', { unit: 'box', capacity: 10, expectedVersion: 'old' }, {})).rejects.toBe(conflict);
    expect(api.put).toHaveBeenCalledTimes(1);
    expect(api.get).not.toHaveBeenCalled();
  });
});
