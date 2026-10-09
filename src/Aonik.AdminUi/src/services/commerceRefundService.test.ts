import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import type { RefundRequest } from '@/types/commerceRefunds';
import { commerceRefundService as service } from './commerceRefundService';

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn() } }));

const request: RefundRequest = { refundId: 'stable-request', reason: 'Damaged package',
  selections: [{ componentId: 'item:recorded-id', amount: null, fullRemaining: true }], expectedPreviewVersion: 'original-version' };

describe('operator refund transport', () => {
  beforeEach(() => { vi.clearAllMocks(); vi.mocked(api.get).mockResolvedValue({}); vi.mocked(api.post).mockResolvedValue({}); });

  it.each([
    ['context', (config: object) => service.getContext('order', config), 'get', '/commerce/admin/orders/order/refunds'],
    ['one refund', (config: object) => service.getRefund('order', 'refund', config), 'get', '/commerce/admin/orders/order/refunds/refund'],
    ['preview', (config: object) => service.preview('order', request, config), 'post', '/commerce/admin/orders/order/refunds/preview'],
    ['request', (config: object) => service.request('order', request, config), 'post', '/commerce/admin/orders/order/refunds'],
    ['reconcile', (config: object) => service.reconcile('order', 'refund', config), 'post', '/commerce/admin/orders/order/refunds/refund/reconcile'],
  ] as const)('preserves captured tenant and cancellation on %s', async (_label, call, method, route) => {
    const config = { headers: { 'X-Tenant-Id': 'tenant-at-click' }, signal: new AbortController().signal };
    await call(config);
    const calls = vi.mocked(api[method]).mock.calls;
    expect(calls).toHaveLength(1);
    expect(calls[0][0]).toBe(route);
    expect(calls[0].at(-1)).toBe(config);
  });

  it('does not retry an uncertain POST or change its operation, preview or full-remaining selection', async () => {
    const failure = { userMessage: 'Timed out' };
    vi.mocked(api.post).mockRejectedValueOnce(failure);
    const config = { headers: { 'X-Tenant-Id': 'tenant' } };
    await expect(service.request('order', request, config)).rejects.toBe(failure);
    expect(api.post).toHaveBeenCalledExactlyOnceWith('/commerce/admin/orders/order/refunds', request, config);
    await service.request('order', request, config);
    expect(vi.mocked(api.post).mock.calls[1][1]).toBe(request);
  });

  it('sends reconciliation without an invented amount or request body', async () => {
    const config = { signal: new AbortController().signal };
    await service.reconcile('order', 'refund', config);
    expect(api.post).toHaveBeenCalledExactlyOnceWith('/commerce/admin/orders/order/refunds/refund/reconcile', undefined, config);
  });
});
