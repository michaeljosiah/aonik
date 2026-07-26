// Pure formatter behind <SignedAmount/> (Spec 073 §5) — kept free of React so
// the node-environment vitest suite can exercise it directly.

/**
 * One formatter for every price delta in the commerce series:
 * `+£2.50` / `−£2.00` (U+2212 minus) / `included` for exactly zero.
 * Unknown ISO codes fall back to `CODE 2.50` rather than throwing.
 */
export function formatSignedAmount(amount: number, currency: string): string {
  if (amount === 0) return 'included';
  const sign = amount > 0 ? '+' : '−';
  return `${sign}${formatUnsignedAmount(Math.abs(amount), currency)}`;
}

/** Unsigned money in the given currency (e.g. `£95.00`), same fallback rule. */
export function formatUnsignedAmount(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency,
      currencyDisplay: 'narrowSymbol',
    }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}
