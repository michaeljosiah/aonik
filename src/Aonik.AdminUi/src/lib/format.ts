// Shared row formatting for the customer detail's tabs. Extracted from CustomerDetailPage
// so the spine and commerce tabs render identical dates and amounts to the page's own
// cards — two copies would drift (Spec 081).

export function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

/**
 * Date AND time. Use wherever the question is "how recently did this happen" rather than
 * "on what day" — cart activity being the case that forced it: several sessions touched on
 * the same day are indistinguishable by date alone, so a stuck cart looks like a live one.
 */
export function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Money in the given currency at that currency's OWN precision; an unknown code falls back
 * to `CODE 1,234.50`.
 *
 * Deliberately no `maximumFractionDigits: 0`. Commerce totals carry real minor units —
 * fractional prices, discounts and tax — so rounding rendered GBP 95.50 as "£96", reporting
 * an amount the order does not have. Intl already omits decimals for zero-decimal
 * currencies like JPY, so the default is right for both.
 */
export function formatCurrency(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toLocaleString(undefined, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;
  }
}
