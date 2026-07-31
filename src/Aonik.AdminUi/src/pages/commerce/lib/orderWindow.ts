// KPI summarisation over the LOADED page of orders (Spec 083 §2). Pure and tested, because a
// KPI is the most quotable thing on a screen and the easiest to compute into a lie.
//
// Two refusals are deliberate:
//
//   * MIXED CURRENCIES ARE NOT SUMMED. There is no rate here, so adding GBP to NGN would
//     invent one. A window spanning several currencies reports the count instead of a total —
//     showing the largest currency's figure would silently drop the others.
//
//   * "AWAITING FULFILMENT" IS NOT REPORTED. The spec names it, but `DeriveFulfilment`
//     (AdminStorefrontService.cs:731) returns only "Unfulfilled" or "Cancelled" — there is no
//     Fulfilled value — so that count is exactly "every order that was not cancelled". A tile
//     equal to the order count carries no information while implying fulfilment is tracked.
//     Awaiting PAYMENT is reported instead: it is derivable, actionable, and true.

import { formatCurrency } from '@/lib/format';

export interface OrderWindowRow {
  orderStatus: string;
  paymentStatus: string;
  currency: string;
  total: number;
}

/**
 * Spine statuses after which no payment is coming. An order can be cancelled while its
 * durable charge summary still reads Pending, so payment status alone would count it as
 * outstanding work — an action presented as doable that can never complete.
 */
const TERMINAL = new Set(['Cancelled', 'Failed', 'Expired']);

export interface OrderWindowSummary {
  /** Formatted total of captured orders, or a currency count when the window is mixed. */
  paidRevenue: string;
  /** Mean captured order, same currency rule. */
  averageOrder: string;
  /** Orders in the window whose payment has not been captured. */
  awaitingPayment: number;
  /** Caption for the money tiles — names the window, and the spread when it matters. */
  moneyCaption: string;
}

const CAPTURED = 'Captured';
const NONE = '—';

export function summariseOrderWindow(rows: readonly OrderWindowRow[]): OrderWindowSummary {
  const paid = rows.filter((row) => row.paymentStatus === CAPTURED);
  const awaitingPayment = rows.filter(
    (row) => row.paymentStatus !== CAPTURED && !TERMINAL.has(row.orderStatus),
  ).length;

  const byCurrency = new Map<string, number>();
  for (const row of paid) {
    byCurrency.set(row.currency, (byCurrency.get(row.currency) ?? 0) + row.total);
  }

  if (byCurrency.size === 0) {
    return {
      paidRevenue: NONE,
      averageOrder: NONE,
      awaitingPayment,
      moneyCaption: 'this page',
    };
  }

  if (byCurrency.size > 1) {
    return {
      paidRevenue: `${byCurrency.size} currencies`,
      averageOrder: `${byCurrency.size} currencies`,
      awaitingPayment,
      moneyCaption: 'this page — not summed',
    };
  }

  const [currency, total] = [...byCurrency.entries()][0];
  return {
    paidRevenue: formatCurrency(total, currency),
    averageOrder: formatCurrency(total / paid.length, currency),
    awaitingPayment,
    moneyCaption: `this page · ${currency}`,
  };
}
