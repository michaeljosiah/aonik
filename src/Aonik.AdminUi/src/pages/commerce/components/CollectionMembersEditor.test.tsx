import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import type { AdminCollectionDto, AdminCollectionItemDto } from '@/types/commerce';
import { CollectionMembersEditor } from './CollectionMembersEditor';

vi.mock('@/services/commerceStorefrontService', () => ({ commerceStorefrontService: { replaceCollectionItems: vi.fn() } }));
vi.mock('@/services/commerceCatalogService', () => ({ commerceCatalogService: { listProducts: vi.fn() } }));

const item = (productId: string, overrides: Partial<AdminCollectionItemDto>): AdminCollectionItemDto => ({
  productId, name: productId, slug: productId, rank: 1, status: 'Active', isPriceable: null, unitPrice: null, currency: null, ...overrides,
});
const collection: AdminCollectionDto = { id: 'extras', slug: 'extras', title: 'Extras', subtitle: null, kind: 'Curated', sortOrder: 0,
  isActive: true, items: [item('Staged dish', { status: 'Draft', rank: 3 }), item('Unpriceable extra', { isPriceable: false, rank: 1 }), item('Unknown extra', { rank: 2 })] };

function render(canWrite: boolean) {
  return renderToStaticMarkup(<CollectionMembersEditor collection={collection} isExtras extras={{ rows: [], skipped: 1 }} previewLoading={false}
    previewError={null} canWrite={canWrite} onSaved={vi.fn()} onDirtyChange={vi.fn()} onSavingChange={vi.fn()} isCurrent={() => true} requestConfig={{}} />);
}

describe('collection member editing', () => {
  it('retains all staged members and displays their distinct server pricing states in rank order', () => {
    const html = render(true);
    expect(html.indexOf('Unpriceable extra')).toBeLessThan(html.indexOf('Unknown extra'));
    expect(html.indexOf('Unknown extra')).toBeLessThan(html.indexOf('Staged dish'));
    expect(html).toContain('Draft — staged');
    expect(html).toContain('Unpriceable — skipped publicly');
    expect(html).toContain('Price not evaluated');
    expect(html).toContain('Save members');
    expect(html).toContain('Move Unknown extra up');
    expect(html).not.toContain('£0.00');
  });

  it('omits every membership mutation control for a read-only administrator', () => {
    const html = render(false);
    expect(html).toContain('Staged dish');
    expect(html).toContain('Unpriceable extra');
    expect(html).not.toContain('Add member');
    expect(html).not.toContain('>Save members</button>');
    expect(html).not.toContain('Remove</button>');
    expect(html).not.toContain('Move Unknown extra');
  });
});
