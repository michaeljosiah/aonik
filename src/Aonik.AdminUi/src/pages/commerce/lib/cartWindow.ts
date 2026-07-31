// KPI summarisation over the LOADED page of carts (Spec 083 §3). Pure and tested, and bound
// by the same two rules as the orders window: mixed currencies are never summed, and every
// figure describes the window rather than the store.
//
// "Open value" counts OPEN carts only. A checked-out cart's value has already become an
// order, and an abandoned one is not value that exists — pooling them would inflate the
// number an operator is most likely to quote.

import { formatCurrency } from '@/lib/format';

import { cartBlocked, type CartBoxMetaLike } from './cartState';

export interface CartWindowRow {
  status: string;
  currency: string;
  total: number;
  boxMeta: CartBoxMetaLike | null;
}

export interface CartWindowSummary {
  /** Formatted total of OPEN carts, or a currency count when the window is mixed. */
  openValue: string;
  /** Open carts that cannot check out right now. */
  blocked: number;
  abandoned: number;
  moneyCaption: string;
}

const OPEN = 'Open';
const ABANDONED = 'Abandoned';
const NONE = '—';

export function summariseCartWindow(rows: readonly CartWindowRow[]): CartWindowSummary {
  const open = rows.filter((row) => row.status === OPEN);
  const abandoned = rows.filter((row) => row.status === ABANDONED).length;
  // Only OPEN carts can be blocked from checking out; a checked-out cart already did, and a
  // stale boxMeta on a frozen cart must not be counted as a live problem.
  const blocked = open.filter((row) => cartBlocked(row.boxMeta).blocked).length;

  const byCurrency = new Map<string, number>();
  for (const row of open) {
    byCurrency.set(row.currency, (byCurrency.get(row.currency) ?? 0) + row.total);
  }

  if (byCurrency.size === 0) {
    return { openValue: NONE, blocked, abandoned, moneyCaption: 'this page' };
  }
  if (byCurrency.size > 1) {
    return {
      openValue: `${byCurrency.size} currencies`,
      blocked,
      abandoned,
      moneyCaption: 'this page — not summed',
    };
  }

  const [currency, total] = [...byCurrency.entries()][0];
  return {
    openValue: formatCurrency(total, currency),
    blocked,
    abandoned,
    moneyCaption: `this page · ${currency}`,
  };
}
