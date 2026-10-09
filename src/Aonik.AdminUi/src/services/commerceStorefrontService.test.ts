import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/lib/api';
import { commerceCatalogService } from './commerceCatalogService';
import { commerceStorefrontService as storefront } from './commerceStorefrontService';

vi.mock('@/lib/api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

describe('storefront administration transport', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    vi.mocked(api.put).mockResolvedValue({});
    vi.mocked(api.post).mockResolvedValue({});
  });

  it.each([
    ['order detail', (config: object) => storefront.getStorefrontOrder('order', config), 'get'],
    ['order fulfilment', (config: object) => storefront.updateOrderFulfilment('order', 'Cooking', 'version', config), 'put'],
    ['calendar read', (config: object) => storefront.getFulfilmentCalendar(config), 'get'],
    ['calendar write', (config: object) => storefront.upsertFulfilmentCalendar({ timezone: 'Europe/London', deliveryDays: ['Friday'], cutoffLocalTime: '12:00', leadDays: 2, blackoutDates: [], isActive: true }, config), 'put'],
    ['public promise', (config: object) => storefront.getPublicDelivery(config), 'get'],
    ['collections', (config: object) => storefront.listCollections(config), 'get'],
    ['collection detail', (config: object) => storefront.getCollection('featured', config), 'get'],
    ['collection create', (config: object) => storefront.createCollection({ slug: 'featured', title: 'Featured' }, config), 'post'],
    ['collection update', (config: object) => storefront.updateCollection('featured', { title: 'Featured', isActive: false }, config), 'put'],
    ['collection membership', (config: object) => storefront.replaceCollectionItems('featured', [], config), 'put'],
    ['facet groups', (config: object) => storefront.listFacetGroups(config), 'get'],
    ['facet create', (config: object) => storefront.createFacetGroup({ key: 'diet', label: 'Diet', matchKind: 'Tag', optionsJson: '[]' }, config), 'post'],
    ['facet update', (config: object) => storefront.updateFacetGroup('diet', { label: 'Diet', isActive: false }, config), 'put'],
    ['public config', (config: object) => storefront.getPublicStorefrontConfig(config), 'get'],
    ['config update', (config: object) => storefront.updateStorefrontConfig({ recommendedChoiceLabel: '' }, config), 'put'],
    ['public extras', (config: object) => storefront.getPublicExtras(config), 'get'],
    ['product picker', (config: object) => commerceCatalogService.listProducts({ page: 2, search: 'A&B' }, config), 'get'],
  ] as const)('keeps the captured tenant and cancellation signal on %s', async (_name, call, method) => {
    const controller = new AbortController();
    const config = { headers: { 'X-Tenant-Id': 'tenant-at-request-start' }, signal: controller.signal };

    await call(config);

    const calls = vi.mocked(api[method]).mock.calls;
    expect(calls).toHaveLength(1);
    expect(calls[0].at(-1)).toBe(config);
  });

  it('preserves an explicit empty membership list and propagates rejection without retry', async () => {
    const error = { userMessage: 'The membership changed. Reload it.' };
    vi.mocked(api.put).mockRejectedValueOnce(error);
    const config = { headers: { 'X-Tenant-Id': 'original-tenant' } };

    await expect(storefront.replaceCollectionItems('collection', [], config)).rejects.toBe(error);

    expect(api.put).toHaveBeenCalledExactlyOnceWith('/commerce/admin/collections/collection/items', { items: [] }, config);
  });
});
