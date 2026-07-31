import { describe, expect, it } from 'vitest';

import { summariseCartWindow } from './cartWindow';
import { summariseOrderWindow } from './orderWindow';

describe('summariseOrderWindow', () => {
  const gbp = (total: number, paymentStatus = 'Captured', orderStatus = 'Complete') => ({
    orderStatus,
    paymentStatus,
    currency: 'GBP',
    total,
  });

  it('sums captured orders only', () => {
    const summary = summariseOrderWindow([gbp(100), gbp(50), gbp(999, 'Pending')]);
    expect(summary.paidRevenue).toContain('150');
    expect(summary.awaitingPayment).toBe(1);
  });

  it('never sums across currencies — it reports the dominant one and what it EXCLUDES', () => {
    const summary = summariseOrderWindow([
      gbp(100),
      gbp(50),
      { orderStatus: 'Complete', paymentStatus: 'Captured', currency: 'NGN', total: 45000 },
    ]);
    expect(summary.paidRevenue).toContain('150');          // GBP only — never 45,150
    expect(summary.excludedOrders).toBe(1);
    expect(summary.moneyCaption).toMatch(/GBP only, excluding 1 order in other currencies/);
  });

  it('picks the dominant currency by ORDER COUNT, not by total', () => {
    // One huge order in a minor currency must not relabel the window as that currency.
    const summary = summariseOrderWindow([
      gbp(10),
      gbp(10),
      { orderStatus: 'Complete', paymentStatus: 'Captured', currency: 'NGN', total: 9_000_000 },
    ]);
    expect(summary.moneyCaption).toContain('GBP');
    expect(summary.paidRevenue).toContain('20');
  });

  it('averages over the orders that made up the total, not the whole window', () => {
    // Dividing GBP 150 by 3 captured orders would report an average no order has.
    const summary = summariseOrderWindow([
      gbp(100),
      gbp(50),
      { orderStatus: 'Complete', paymentStatus: 'Captured', currency: 'NGN', total: 45000 },
    ]);
    expect(summary.averageOrder).toContain('75');
  });

  it('reports nothing excluded in a single-currency window', () => {
    expect(summariseOrderWindow([gbp(10)]).excludedOrders).toBe(0);
  });

  it('averages over captured orders, not the whole window', () => {
    const summary = summariseOrderWindow([gbp(100), gbp(200), gbp(9999, 'Pending')]);
    expect(summary.averageOrder).toContain('150');
  });

  it('reports no revenue rather than zero when nothing is captured', () => {
    // "£0.00" would assert the store took nothing; "—" says this window has no captures.
    const summary = summariseOrderWindow([gbp(80, 'Pending')]);
    expect(summary.paidRevenue).toBe('—');
    expect(summary.averageOrder).toBe('—');
    expect(summary.awaitingPayment).toBe(1);
  });

  it('EXCLUDES terminal orders from awaiting payment', () => {
    // An order can be cancelled while its durable charge summary still reads Pending, so
    // payment status alone would present work that can never complete as outstanding.
    const summary = summariseOrderWindow([
      gbp(80, 'Pending', 'Pending'),
      gbp(80, 'Pending', 'Cancelled'),
      gbp(80, 'Pending', 'Failed'),
      gbp(80, 'Pending', 'Expired'),
    ]);
    expect(summary.awaitingPayment).toBe(1);
  });

  it('handles an empty window', () => {
    expect(summariseOrderWindow([])).toMatchObject({
      paidRevenue: '—',
      awaitingPayment: 0,
    });
  });

  it('names the currency in the caption when there is exactly one', () => {
    expect(summariseOrderWindow([gbp(10)]).moneyCaption).toBe('this page · GBP');
  });
});

describe('summariseCartWindow', () => {
  const open = (total: number, boxMeta = null as null | { size: number; filled: number; drift: boolean }) => ({
    status: 'Open',
    currency: 'GBP',
    total,
    boxMeta,
  });

  it('values OPEN carts only — checked-out value is already an order', () => {
    const summary = summariseCartWindow([
      open(40),
      { status: 'CheckedOut', currency: 'GBP', total: 900, boxMeta: null },
      { status: 'Abandoned', currency: 'GBP', total: 700, boxMeta: null },
    ]);
    expect(summary.openValue).toContain('40');
    expect(summary.abandoned).toBe(1);
  });

  it('counts blocked carts only among OPEN ones', () => {
    // A frozen cart's stale boxMeta must not read as a live problem.
    const summary = summariseCartWindow([
      open(10, { size: 6, filled: 2, drift: false }),
      { status: 'CheckedOut', currency: 'GBP', total: 10, boxMeta: { size: 6, filled: 1, drift: true } },
    ]);
    expect(summary.blocked).toBe(1);
  });

  it('refuses to sum open value across currencies', () => {
    const summary = summariseCartWindow([
      open(40),
      { status: 'Open', currency: 'NGN', total: 9000, boxMeta: null },
    ]);
    expect(summary.openValue).toBe('2 currencies');
    expect(summary.moneyCaption).toMatch(/not summed/);
  });

  it('reports no open value rather than zero when nothing is open', () => {
    const summary = summariseCartWindow([
      { status: 'Abandoned', currency: 'GBP', total: 700, boxMeta: null },
    ]);
    expect(summary.openValue).toBe('—');
    expect(summary.blocked).toBe(0);
  });
});
