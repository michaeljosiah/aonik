import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { DeliveryCapacityTable } from './DeliveryCapacityTable';

describe('capacity table', () => {
  const rows = [{ deliveryDate: '2026-10-15', capacity: 0, occupied: 0, unit: 'box', version: 'v1' }];
  const dates = ['2026-10-15', '2026-10-16'];

  it('distinguishes an explicit zero budget from an unconfigured date and omits write controls for readers', () => {
    const html = renderToStaticMarkup(<DeliveryCapacityTable dates={dates} rows={rows} availability={null}
      canWrite={false} saving={false} onSelect={vi.fn()} />);
    expect(html).toContain('>0</td>');
    expect(html).toContain('Unconfigured');
    expect(html).toContain('Unknown');
    expect(html).not.toContain('<button');
    expect(html).not.toContain('Available');
  });

  it('uses server availability even when spare configured capacity exists', () => {
    const html = renderToStaticMarkup(<DeliveryCapacityTable dates={dates} rows={[{ ...rows[0], capacity: 100 }]} availability={{
      earliestDeliveryDate: null, timezone: 'Europe/London', fromDate: dates[0], toDate: dates[1], dates: [], serverNowUtc: null,
      availability: [{ deliveryDate: dates[0], status: 'no_delivery' }, { deliveryDate: dates[1], status: 'unknown' }],
    }} canWrite saving={false} onSelect={vi.fn()} />);
    expect(html).toContain('No delivery');
    expect(html).toContain('Unknown');
    expect(html).not.toContain('Available');
    expect(html).toContain('<button');
  });

  it('disables row changes while a capacity write is pending', () => {
    const html = renderToStaticMarkup(<DeliveryCapacityTable dates={[dates[0]]} rows={rows} availability={null}
      canWrite saving onSelect={vi.fn()} />);
    expect(html).toContain('disabled=""');
  });
});
