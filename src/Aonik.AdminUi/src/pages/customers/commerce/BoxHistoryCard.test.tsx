import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { BoxHistoryCard } from './BoxHistoryCard';

describe('recorded customer history', () => {
  it('shows the paid reference, recorded preparation and explicit group separately from payment', () => {
    const html = renderToStaticMarkup(<BoxHistoryCard orders={[{
      orderId: 'legacy-id', orderNumber: 'BOX-0042', placedAtUtc: '2026-10-09T12:00:00Z', status: 'Complete',
      paymentStatus: 'Captured', fulfilmentStatus: 'Cooking', historyGroup: 'Upcoming',
      currency: 'GBP', total: 90, boxSize: 6, deliveryDate: '2026-10-15', discountCode: 'AUTUMN', discountTotal: 5,
      selections: [{ productVariantId: 'retired', quantity: 6, sku: 'OLD-SKU', name: 'Original recipe',
        isSignature: true, orderItemIndex: 0, personalisationSummary: 'Mild' }],
    }]} />);
    for (const text of ['BOX-0042', 'Captured', 'Cooking', 'Upcoming', 'Original recipe', 'Signature', 'Mild', 'AUTUMN']) expect(html).toContain(text);
    expect(html).not.toContain('ORD-');
  });
});
