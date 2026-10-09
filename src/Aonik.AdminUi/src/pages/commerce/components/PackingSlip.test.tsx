import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import type { AdminOrderPackingDto } from '@/types/commerce';
import { PackingSlip } from './PackingSlip';

const packing: AdminOrderPackingDto = {
  orderId: 'order-1', boxSize: 6, deliveryDate: '2026-10-25', timezone: 'Europe/London',
  recipient: { name: 'Sam Recipient', phone: '020 2222 2222' },
  address: { line1: '10 Kitchen Road', line2: null, city: 'London', region: null, postcode: 'SW1A 1AA', countryCode: 'GB' },
  notes: 'Ring the bell\nLeave with reception',
  gift: { hidePrices: true, includeGreetingCard: true, greetingCardMessage: 'Happy birthday!\n<script>literal text</script>' },
  items: [
    { itemIndex: 5, itemType: 'ProductPurchase', name: 'Food box', sku: 'BOX', quantity: 1 },
    { itemIndex: 6, itemType: 'GreetingCard', name: 'Greeting card', sku: null, quantity: 1 },
  ],
  selections: [{ orderItemIndex: 5, productVariantId: 'dish-1', sku: 'DISH-1', quantity: 6, name: 'Jollof', personalisationSummary: 'Mild\nNo garnish' }],
};

describe('recipient packing slip', () => {
  it('renders quantities, preparation and escaped multiline gift text without financial fields', () => {
    const html = renderToStaticMarkup(<PackingSlip packing={packing} />);
    for (const text of ['Gift box packing slip', 'Sam Recipient', '10 Kitchen Road', 'Food box', '6 × Jollof', 'Mild\nNo garnish',
      'Happy birthday!\n&lt;script&gt;literal text&lt;/script&gt;']) expect(html).toContain(text);
    expect(html).not.toContain('<script>');
    expect(html).not.toContain('Subtotal');
    expect(html).not.toContain('Charges');
    expect(html).not.toContain('£');
    expect(html).not.toContain('Purchaser');
  });

  it('renders original line and total amounts only from the optional server price envelope', () => {
    const html = renderToStaticMarkup(<PackingSlip packing={{ ...packing,
      gift: { ...packing.gift!, hidePrices: false },
      items: [...packing.items, { itemIndex: 7, itemType: 'DeliveryFee', name: 'Delivery', sku: 'delivery', quantity: 1 }],
      prices: { charge: { currency: 'GBP', subtotal: 98, discountTotal: 10, discountCode: 'TEN', taxTotal: 2, total: 97 },
        items: [{ itemIndex: 5, unitPrice: 95, amount: 95 }, { itemIndex: 6, unitPrice: 3, amount: 3 }, { itemIndex: 7, unitPrice: 7, amount: 7 }] },
    }} />);
    expect(html).toContain('£95.00');
    expect(html).toContain('£3.00');
    expect(html).toContain('£98.00');
    expect(html).toContain('Delivery: £7.00');
    expect(html).toContain('Tax: £2.00');
    expect(html).toContain('Total: £97.00');
    expect(html).not.toContain('1 × Delivery');
    expect(html).toContain('Charges');
  });

  it('preserves unmatched preparation snapshots and shows a blank card instruction', () => {
    const html = renderToStaticMarkup(<PackingSlip packing={{ ...packing,
      gift: { ...packing.gift!, greetingCardMessage: null },
      selections: [{ ...packing.selections[0], orderItemIndex: 99 }],
    }} />);
    expect(html).toContain('Unmatched preparations');
    expect(html).toContain('6 × Jollof');
    expect(html).toContain('Include a blank greeting card.');
  });

  it('does not invent a gift or delivery for historical ordinary orders', () => {
    const html = renderToStaticMarkup(<PackingSlip packing={{ ...packing, gift: null, deliveryDate: null,
      recipient: null, address: null, notes: null, items: [packing.items[0]] }} />);
    expect(html).not.toContain('Gift box');
    expect(html).not.toContain('Greeting card');
    expect(html).not.toContain('Recipient');
    expect(html).toContain('Food box');
  });
});
