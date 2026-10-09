import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { DiscountProductSelection } from './DiscountProductSelection';

vi.mock('@/services/commerceCatalogService', () => ({ commerceCatalogService: { listProducts: vi.fn() } }));

describe('discount product selection', () => {
  it('keeps previously selected products visible before catalog results load', () => {
    const changed = vi.fn();
    const html = renderToStaticMarkup(<DiscountProductSelection selectedIds={['retired-product', 'another-page-product']}
      onChange={changed} disabled={false} />);
    expect(html).toContain('2 of 200 selected');
    expect(html).toContain('retired-product');
    expect(html).toContain('another-page-product');
    expect(html).toContain('Remove retired-product');
    expect(changed).not.toHaveBeenCalled();
  });

  it('disables search and selection removal while saving', () => {
    const html = renderToStaticMarkup(<DiscountProductSelection selectedIds={['retired-product']}
      onChange={vi.fn()} disabled />);
    expect(html).toMatch(/<input[^>]*disabled/);
    expect(html).toMatch(/<button[^>]*disabled[^>]*aria-label="Remove retired-product"/);
  });
});
