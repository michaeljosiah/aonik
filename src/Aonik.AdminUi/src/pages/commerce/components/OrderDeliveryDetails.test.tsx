import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import type { OrderDeliveryDto } from '@/types/commerce';
import { OrderDeliveryDetails } from './OrderDeliveryDetails';

const delivery: OrderDeliveryDto = {
  purchaser: { firstName: 'Ada', lastName: 'Cook', email: 'ada@example.com', phone: '020 1111 1111' },
  recipient: { name: 'Sam Recipient', phone: '020 2222 2222' },
  address: { line1: '10 Kitchen Road', line2: 'Flat 2', city: 'London', region: null, postcode: 'SW1A 1AA', countryCode: 'GB' },
  deliveryDate: '2026-10-25', timezone: 'Europe/London', notes: 'Ring the bell\nLeave with reception',
};

describe('order delivery snapshot', () => {
  it('shows the recorded gift instructions without removing purchaser details from the finance view', () => {
    const html = renderToStaticMarkup(<OrderDeliveryDetails delivery={{ ...delivery,
      gift: { hidePrices: true, includeGreetingCard: true, greetingCardMessage: 'Happy birthday!\n<em>Love</em>' },
    }} />);
    expect(html).toContain('Prices hidden on the packing slip');
    expect(html).toContain('Include greeting card');
    expect(html).toContain('Happy birthday!\n&lt;em&gt;Love&lt;/em&gt;');
    expect(html).toContain('ada@example.com');
  });

  it('renders purchaser and recipient separately with the recorded address and instructions', () => {
    const html = renderToStaticMarkup(<OrderDeliveryDetails delivery={delivery} />);
    for (const fact of ['Ada Cook', 'ada@example.com', '020 1111 1111', 'Sam Recipient', '020 2222 2222',
      '10 Kitchen Road\nFlat 2\nLondon\nSW1A 1AA\nGB', 'Europe/London', 'Ring the bell\nLeave with reception']) {
      expect(html).toContain(fact);
    }
    expect(html).not.toContain('null');
  });

  it('shows missing historical details without fabricating a date or address', () => {
    const html = renderToStaticMarkup(<OrderDeliveryDetails delivery={null} />);
    expect(html).toContain('No delivery details were recorded for this order.');
    expect(html).not.toContain('Purchaser');
    expect(html).not.toContain('Delivery address');
  });

  it('escapes submitted notes and omits unauthored optional fields', () => {
    const html = renderToStaticMarkup(<OrderDeliveryDetails delivery={{
      ...delivery, address: { ...delivery.address, line2: null }, notes: '<script>alert(1)</script>',
    }} />);
    expect(html).not.toContain('<script>');
    expect(html).toContain('&lt;script&gt;');
    expect(html).not.toContain('Flat 2');
  });
});
