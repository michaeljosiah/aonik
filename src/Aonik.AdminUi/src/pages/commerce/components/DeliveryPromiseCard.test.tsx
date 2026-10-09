import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { formatCalendarDate } from '@/lib/format';
import { DeliveryPromiseCard } from './DeliveryPromiseCard';

describe('saved delivery promise display', () => {
  const promise = { earliestDeliveryDate: '2026-10-15', timezone: 'Europe/London' };

  it('keeps the saved date with an unsaved-change warning instead of previewing a fake promise', () => {
    const html = renderToStaticMarkup(<DeliveryPromiseCard promise={promise} dirty loading={false} error={null} />);
    expect(html).toContain('Unsaved changes');
    expect(html).toContain(formatCalendarDate(promise.earliestDeliveryDate));
    expect(html).toContain('Europe/London');
    expect(html).toContain('saved calendar and capacity');
  });

  it('explains capacity uncertainty when the server has no promise', () => {
    const html = renderToStaticMarkup(<DeliveryPromiseCard promise={null} dirty={false} loading={false} error={null} />);
    expect(html).toContain('No delivery promise');
    expect(html).toContain('missing capacity and fully booked dates');
  });

  it('keeps transport failures distinct from a valid no-promise result', () => {
    const html = renderToStaticMarkup(<DeliveryPromiseCard promise={promise} dirty={false} loading={false} error="Could not check delivery" />);
    expect(html).toContain('Could not check delivery');
    expect(html).not.toContain(formatCalendarDate(promise.earliestDeliveryDate));
    expect(html).not.toContain('No delivery promise');
  });

  it('withholds a previous response while refreshing after a save', () => {
    const html = renderToStaticMarkup(<DeliveryPromiseCard promise={promise} dirty={false} loading error={null} />);
    expect(html).toContain('Checking the saved promise');
    expect(html).not.toContain(formatCalendarDate(promise.earliestDeliveryDate));
  });
});
