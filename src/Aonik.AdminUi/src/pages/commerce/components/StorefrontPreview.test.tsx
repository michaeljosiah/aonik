import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { StorefrontConfigDto } from '@/types/commerce';
import { StorefrontPreview } from './StorefrontPreview';

const config: StorefrontConfigDto = {
  currency: 'GBP', recommendedChoiceLabel: 'Saved recommendation', resultsPageSize: 8,
  backToTopTrigger: { type: 'cardIndex', value: 10, retained: 'custom-property' },
  delivery: { listAmount: 7.95, chargedAmount: 0 }, defaultBoxSlug: null,
  extrasCollectionSlug: 'extras', box: null,
};

describe('saved storefront preview', () => {
  it('uses the saved label and typed trigger while explicitly marking unsaved form changes', () => {
    const html = renderToStaticMarkup(<StorefrontPreview config={config} pending />);
    expect(html).toContain('Saved recommendation');
    expect(html).toContain('custom-property');
    expect(html).toContain('Unsaved changes are not shown here');
  });

  it('renders exactly zero as Free, retains the struck list amount and shows an honest absent plan', () => {
    const html = renderToStaticMarkup(<StorefrontPreview config={config} pending={false} />);
    expect(html).toContain('Free');
    expect(html).toContain('£7.95</s>');
    expect(html).toContain('No default box plan is live');
    expect(html).not.toContain('Example:');
  });

  it('shows the charged amount and actual preset arithmetic without deriving a saving', () => {
    const html = renderToStaticMarkup(<StorefrontPreview pending={false} config={{ ...config,
      delivery: { listAmount: 7.95, chargedAmount: 5.95 },
      box: { currency: 'GBP', minSize: 6, maxSize: 30, perSpacePrice: 15,
        presets: [{ size: 6, price: 95, badge: 'Starter', blurb: 'Authored wording', saving: null }] } }} />);
    expect(html).toContain('£5.95</strong>');
    expect(html).toContain('£100.95');
    expect(html).toContain('Starter');
    expect(html).toContain('Authored wording');
    expect(html).not.toContain('Authored saving:');
    expect(html).not.toContain('>Free<');
  });

  it('preserves a different plan currency and authored saving without adding unlike currencies', () => {
    const html = renderToStaticMarkup(<StorefrontPreview pending={false} config={{ ...config,
      box: { currency: 'EUR', minSize: 6, maxSize: 30, perSpacePrice: 15,
        presets: [{ size: 6, price: 95, badge: null, blurb: null, saving: 4 }] } }} />);
    expect(html).toContain('€95.00');
    expect(html).toContain('€4.00');
    expect(html).toContain('Authored saving:');
    expect(html).toContain('The box plan uses EUR');
    expect(html).not.toContain('Example:');
    expect(html).not.toContain('£95.00');
  });

  it('escapes authored labels and custom JSON instead of rendering supplied markup', () => {
    const html = renderToStaticMarkup(<StorefrontPreview pending={false} config={{ ...config,
      recommendedChoiceLabel: '<script>bad()</script>', backToTopTrigger: { text: '<img src=x>' } }} />);
    expect(html).not.toContain('<script>');
    expect(html).not.toContain('<img src=x>');
    expect(html).toContain('&lt;script&gt;');
  });
});
