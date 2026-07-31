// KPI summarisation over the LOADED page of orders (Spec 083 §2). Pure and tested, because a
// KPI is the most quotable thing on a screen and the easiest to compute into a lie.
//
// Two refusals are deliberate:
//
//   * MIXED CURRENCIES ARE NOT SUMMED. There is no rate here, so adding GBP to NGN would
//     invent one. A window spanning several currencies reports the DOMINANT currency's
//     aggregate and says how many orders it excludes (the Spec 081/084 rule) — the exclusion
//     note is what makes it honest, and it beats refusing a number the operator can use.
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
  /** Formatted total of captured orders in the dominant currency. */
  paidRevenue: string;
  /** Mean captured order in the dominant currency. */
  averageOrder: string;
  /** Orders in the window whose payment has not been captured and can still arrive. */
  awaitingPayment: number;
  /**
   * Caption for the money tiles. Kept SHORT — it renders inside `KpiTile.delta`, which is a
   * shrink-0 inline pill, so a sentence here widens the tile past its column and pushes into
   * its neighbours. The exclusion is reported by `excludedOrders` for the caller to render as
   * wrappable text instead.
   */
  moneyCaption: string;
  /** Captured orders NOT counted in the figures above; 0 in a single-currency window. */
  excludedOrders: number;
}

const CAPTURED = 'Captured';
const NONE = '—';

/**
 * @param windowLabel What the rows ARE, named in the caption. A paged table can say "this
 * page" because the table below is the page; a dashboard cannot, because its figures cover a
 * fetch the operator never sees in full — "latest 25 orders" is the only honest phrasing there.
 */
export function summariseOrderWindow(
  rows: readonly OrderWindowRow[],
  windowLabel = 'this page',
): OrderWindowSummary {
  const paid = rows.filter((row) => row.paymentStatus === CAPTURED);
  const awaitingPayment = rows.filter(
    (row) => row.paymentStatus !== CAPTURED && !TERMINAL.has(row.orderStatus),
  ).length;

  // Totals AND counts per currency: the mean must divide by the orders that made up the
  // total, not by every captured order in the window.
  const byCurrency = new Map<string, { total: number; count: number }>();
  for (const row of paid) {
    const entry = byCurrency.get(row.currency) ?? { total: 0, count: 0 };
    byCurrency.set(row.currency, { total: entry.total + row.total, count: entry.count + 1 });
  }

  if (byCurrency.size === 0) {
    return {
      paidRevenue: NONE,
      averageOrder: NONE,
      awaitingPayment,
      moneyCaption: windowLabel,
      excludedOrders: 0,
    };
  }

  // Dominant = most ORDERS, not the largest total — a single large order in a minor currency
  // must not relabel the window, and order count is the currency-free comparison.
  const [currency, { total, count }] = [...byCurrency.entries()].sort(
    (a, b) => b[1].count - a[1].count || a[0].localeCompare(b[0]),
  )[0];
  const excludedOrders = paid.length - count;

  return {
    paidRevenue: formatCurrency(total, currency),
    averageOrder: formatCurrency(total / count, currency),
    awaitingPayment,
    moneyCaption: `${windowLabel} · ${currency}`,
    excludedOrders,
  };
}
