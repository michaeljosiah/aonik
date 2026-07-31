import { describe, expect, it } from 'vitest';

import type { OrderListItem } from '@/types';

import {
  ORDER_TYPE_BILL_PAYMENT,
  ORDER_TYPE_PRODUCT_PURCHASE,
  ORDER_TYPE_REMITTANCE,
  dominantCurrencyTotal,
  storefrontSubset,
  storefrontValue,
  valueByCurrency,
} from './spineDerivations';

function order(
  orderType: string,
  totalAmountIn: number,
  originCurrency = 'GBP',
): OrderListItem {
  return {
    orderId: crypto.randomUUID(),
    orderType,
    status: 'Complete',
    payerName: 'Test',
    originCurrency,
    totalAmountIn,
    createdAt: '2026-07-01T00:00:00Z',
  };
}

describe('storefrontSubset', () => {
  it('keeps only ProductPurchase orders', () => {
    const orders = [
      order(ORDER_TYPE_PRODUCT_PURCHASE, 95),
      order(ORDER_TYPE_BILL_PAYMENT, 40),
      order(ORDER_TYPE_REMITTANCE, 500),
      order(ORDER_TYPE_PRODUCT_PURCHASE, 60),
    ];
    const subset = storefrontSubset(orders);
    expect(subset).toHaveLength(2);
    expect(subset.every((o) => o.orderType === ORDER_TYPE_PRODUCT_PURCHASE)).toBe(true);
  });

  it('is empty when the customer has bought no boxes', () => {
    expect(storefrontSubset([order(ORDER_TYPE_BILL_PAYMENT, 40)])).toEqual([]);
  });
});

describe('valueByCurrency', () => {
  it('never sums across currencies', () => {
    const totals = valueByCurrency([
      order(ORDER_TYPE_PRODUCT_PURCHASE, 95, 'GBP'),
      order(ORDER_TYPE_BILL_PAYMENT, 40, 'GBP'),
      order(ORDER_TYPE_REMITTANCE, 90_000, 'NGN'),
    ]);
    expect(totals).toEqual([
      { currency: 'NGN', amount: 90_000 },
      { currency: 'GBP', amount: 135 },
    ]);
  });

  it('orders by amount descending, then currency, so rendering is stable', () => {
    const totals = valueByCurrency([
      order(ORDER_TYPE_PRODUCT_PURCHASE, 50, 'USD'),
      order(ORDER_TYPE_PRODUCT_PURCHASE, 50, 'EUR'),
      order(ORDER_TYPE_PRODUCT_PURCHASE, 90, 'GBP'),
    ]);
    expect(totals.map((t) => t.currency)).toEqual(['GBP', 'EUR', 'USD']);
  });

  it('ignores rows with no currency rather than bucketing them under empty string', () => {
    const missing = { ...order(ORDER_TYPE_PRODUCT_PURCHASE, 10), originCurrency: '' };
    expect(valueByCurrency([missing])).toEqual([]);
  });

  it('handles the empty case', () => {
    expect(valueByCurrency([])).toEqual([]);
  });
});

describe('storefrontValue', () => {
  it('equals the sum of the box-history rows it renders beside — the consistency rule', () => {
    const orders = [
      order(ORDER_TYPE_PRODUCT_PURCHASE, 95),
      order(ORDER_TYPE_PRODUCT_PURCHASE, 60),
      order(ORDER_TYPE_BILL_PAYMENT, 40),   // must NOT count toward storefront value
    ];
    const rendered = storefrontSubset(orders);
    const value = storefrontValue(orders);

    expect(value).toEqual([{ currency: 'GBP', amount: 155 }]);
    expect(value[0].amount).toBe(rendered.reduce((sum, o) => sum + o.totalAmountIn, 0));
  });
});

describe('dominantCurrencyTotal', () => {
  it('reports the single-currency case with nothing omitted', () => {
    expect(dominantCurrencyTotal([{ currency: 'GBP', amount: 155 }])).toEqual({
      total: { currency: 'GBP', amount: 155 },
      otherCurrencyCount: 0,
    });
  });

  it('flags how many currencies a headline figure leaves out instead of faking a sum', () => {
    const result = dominantCurrencyTotal([
      { currency: 'NGN', amount: 90_000 },
      { currency: 'GBP', amount: 135 },
      { currency: 'USD', amount: 20 },
    ]);
    expect(result.total).toEqual({ currency: 'NGN', amount: 90_000 });
    expect(result.otherCurrencyCount).toBe(2);
  });

  it('has nothing to show for an empty list', () => {
    expect(dominantCurrencyTotal([])).toEqual({ total: null, otherCurrencyCount: 0 });
  });
});
