import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { commerceDiscountService, type UpdateDiscountRequest } from './commerceDiscountService';

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

describe('commerce discount transport', () => {
  beforeEach(() => vi.clearAllMocks());

  it('preserves false filters, encodes search and adapts server pagination', async () => {
    vi.mocked(api.get).mockResolvedValue({ items: [], totalCount: 41, page: 2, pageSize: 25 });
    const result = await commerceDiscountService.list({ page: 2, pageSize: 25, search: 'A&B', isActive: false });
    expect(api.get).toHaveBeenCalledWith('/commerce/admin/discounts?page=2&pageSize=25&search=A%26B&isActive=false');
    expect(result).toEqual({ items: [], totalCount: 41, pageNumber: 2, pageSize: 25, totalPages: 2 });
  });

  it('posts only the create contract without inventing active/version fields', async () => {
    const request = { code: 'SAVE', kind: 'Percentage' as const, value: 10, currency: null, maxRedemptions: null, expiresAt: null, eligibleProductIds: null };
    await commerceDiscountService.create(request);
    expect(api.post).toHaveBeenCalledWith('/commerce/admin/discounts', request);
  });

  it('keeps explicit clears and propagates conflict without retrying or changing the native version', async () => {
    const request: UpdateDiscountRequest = { kind: 'Percentage', value: 10, currency: null, maxRedemptions: null,
      expiresAt: null, eligibleProductIds: null, isActive: false, expectedVersion: 'AAAAAAAAB4s=' };
    const conflict = { response: { status: 409, data: { code: 'commerce.discount_conflict' } } };
    vi.mocked(api.put).mockRejectedValue(conflict);
    await expect(commerceDiscountService.update('discount-id', request)).rejects.toBe(conflict);
    expect(api.put).toHaveBeenCalledTimes(1);
    expect(api.put).toHaveBeenCalledWith('/commerce/admin/discounts/discount-id', request);
    expect(request.expectedVersion).toBe('AAAAAAAAB4s=');
  });
});
