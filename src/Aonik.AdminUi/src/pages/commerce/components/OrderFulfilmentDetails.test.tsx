import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { OrderFulfilmentDetails } from './OrderFulfilmentDetails';

vi.mock('@/services/commerceStorefrontService', () => ({ commerceStorefrontService: { updateOrderFulfilment: vi.fn() } }));

describe('staff fulfilment control', () => {
  it('offers only the next explicit stage to a writer and no invented confirmation time', () => {
    const html = renderToStaticMarkup(<OrderFulfilmentDetails orderId="order" canWrite
      fulfilment={{ status: 'Confirmed', version: 'native-version', history: [] }} onUpdated={async () => {}} />);
    expect(html).toContain('Mark as Cooking');
    expect(html).not.toContain('Mark as Delivered');
    expect(html).toContain('No staff progress updates recorded.');
  });

  it('shows recorded progress to read-only staff without a write button', () => {
    const html = renderToStaticMarkup(<OrderFulfilmentDetails orderId="order" canWrite={false}
      fulfilment={{ status: 'OutForDelivery', version: 'v', history: [
        { fromStatus: 'Cooking', toStatus: 'OutForDelivery', actorId: 'staff-42', occurredAtUtc: '2026-10-09T12:00:00Z' },
      ] }} onUpdated={async () => {}} />);
    expect(html).toContain('Out for delivery');
    expect(html).toContain('staff-42');
    expect(html).not.toContain('Mark as');
  });
});
