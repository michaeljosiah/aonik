import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { AdminCollectionDto, AdminCollectionItemDto, ExtrasListDto } from '@/types/commerce';
import { RailPreview } from './RailPreview';

const active: AdminCollectionItemDto = { productId: 'active', name: 'Active dish', slug: 'active', status: 'Active', rank: 1,
  unitPrice: null, currency: null, isPriceable: null };
const draft: AdminCollectionItemDto = { ...active, productId: 'draft', name: 'Draft dish', status: 'Draft', rank: 2 };
const collection: AdminCollectionDto = { id: 'collection', slug: 'featured', title: 'Featured', subtitle: null,
  kind: 'Featured', sortOrder: 1, isActive: true, items: [active, draft] };
const extras: ExtrasListDto = { skipped: 2, rows: [{ productId: 'published', productVariantId: 'variant', slug: 'published', name: 'Published extra',
  description: null, imageUrl: null, tags: [], attributesJson: null, unitPrice: 3, unitSurcharge: 0.5, currency: 'GBP', content: null, optionGroups: [] }] };

describe('collection rail preview', () => {
  it('marks unsaved non-extras membership and hides drafts', () => {
    const html = renderToStaticMarkup(<RailPreview collection={collection} items={collection.items} dirty isExtras={false}
      extras={null} loading={false} error={null} />);
    expect(html).toContain('Unsaved membership preview');
    expect(html).toContain('Local changes only');
    expect(html).toContain('Active dish');
    expect(html).not.toContain('Draft dish');
  });

  it('uses the public extras response even while the local membership differs', () => {
    const html = renderToStaticMarkup(<RailPreview collection={collection} items={[active]} dirty isExtras
      extras={extras} loading={false} error={null} />);
    expect(html).toContain('Published extras rail');
    expect(html).toContain('Published extra');
    expect(html).not.toContain('Active dish');
    expect(html).toContain('omitted and counted 2 unpriceable members');
    expect(html).toContain('£3.00');
    expect(html).toContain('£0.50');
  });

  it('never renders a public rail for an inactive collection', () => {
    const html = renderToStaticMarkup(<RailPreview collection={{ ...collection, isActive: false }} items={collection.items} dirty={false} isExtras
      extras={extras} loading={false} error={null} />);
    expect(html).toContain('public read serves nothing');
    expect(html).not.toContain('>Published extra</p>');
  });

  it('distinguishes configuration/read failure from a genuinely empty rail', () => {
    const unavailable = renderToStaticMarkup(<RailPreview collection={collection} items={collection.items} dirty={false} isExtras={null}
      extras={null} loading={false} error={null} />);
    expect(unavailable).toContain('configuration is unavailable');
    expect(unavailable).not.toContain('Active dish');
    const failed = renderToStaticMarkup(<RailPreview collection={collection} items={collection.items} dirty={false} isExtras
      extras={null} loading={false} error="Preview unavailable" />);
    expect(failed).toContain('Preview unavailable');
    expect(failed).not.toContain('rail is empty');
    const empty = renderToStaticMarkup(<RailPreview collection={collection} items={collection.items} dirty={false} isExtras
      extras={{ rows: [], skipped: 0 }} loading={false} error={null} />);
    expect(empty).toContain('rail is empty');
  });
});
