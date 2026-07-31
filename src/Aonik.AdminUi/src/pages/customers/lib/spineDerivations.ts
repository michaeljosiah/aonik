// Pure derivations over the ORDER SPINE (Spec 081 §5). Everything the customer detail
// shows about a party's orders comes from one dataset, so a profile figure can never
// disagree with the rows rendered beside it — the consistency rule the spec calls out,
// and the same payer-scoped predicate Spec 080's registry counts use.

import type { OrderListItem } from '@/types';

/** The order-type codes the spine ships today. Open by design — the enum is additive. */
export const ORDER_TYPE_PRODUCT_PURCHASE = 'ProductPurchase';
export const ORDER_TYPE_BILL_PAYMENT = 'BillPayment';
export const ORDER_TYPE_REMITTANCE = 'Remittance';
export const ORDER_TYPE_BANK_TRANSFER = 'BankTransfer';
export const ORDER_TYPE_PAYOUT = 'Payout';

/** The storefront slice of a party's spine orders — box purchases and nothing else. */
export function storefrontSubset(orders: readonly OrderListItem[]): OrderListItem[] {
  return orders.filter((o) => o.orderType === ORDER_TYPE_PRODUCT_PURCHASE);
}

export interface CurrencyTotal {
  currency: string;
  amount: number;
}

/**
 * Order value grouped by currency, descending by amount then currency for stability.
 *
 * NEVER a single cross-currency number: a customer who spent £400 and ₦90,000 has two
 * totals, and adding them would invent an exchange rate. Callers that need one headline
 * figure use {@link dominantCurrencyTotal}, which says how many currencies it is omitting.
 */
export function valueByCurrency(orders: readonly OrderListItem[]): CurrencyTotal[] {
  const totals = new Map<string, number>();
  for (const order of orders) {
    const currency = order.originCurrency;
    if (!currency) continue;
    totals.set(currency, (totals.get(currency) ?? 0) + (order.totalAmountIn ?? 0));
  }
  return [...totals.entries()]
    .map(([currency, amount]) => ({ currency, amount }))
    .sort((a, b) => b.amount - a.amount || a.currency.localeCompare(b.currency));
}

/** Storefront (box) value per currency — the box-history rows' own totals, by construction. */
export function storefrontValue(orders: readonly OrderListItem[]): CurrencyTotal[] {
  return valueByCurrency(storefrontSubset(orders));
}

export interface DominantTotal {
  /** Null when there is nothing to show at all. */
  total: CurrencyTotal | null;
  /** How many OTHER currencies exist; > 0 means the headline is a subtotal, not the whole. */
  otherCurrencyCount: number;
}

/**
 * The largest single-currency subtotal plus a count of what it leaves out, so a headline
 * figure can be honest about being partial rather than presenting a fabricated sum
 * (Spec 081 §5).
 */
export function dominantCurrencyTotal(totals: readonly CurrencyTotal[]): DominantTotal {
  if (totals.length === 0) return { total: null, otherCurrencyCount: 0 };
  return { total: totals[0], otherCurrencyCount: totals.length - 1 };
}
