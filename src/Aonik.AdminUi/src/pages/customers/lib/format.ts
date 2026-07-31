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

/** Whole-unit money in the given currency; an unknown code falls back to `CODE 1,234`. */
export function formatCurrency(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currency} ${Math.round(amount).toLocaleString()}`;
  }
}
