// Live FX-quote mini-banner — port of `FxQuote` from
// templates/aonik-admin-starterkit/screens/orders.jsx.
//
// Renders only when a real quote is in hand AND the origin/destination
// currencies actually differ. Reads the rate from `pricingService.getQuote`'s
// response. The template put the input currency on the left ("here's what
// you typed") and the conversion target on the right ("here's the cost") —
// for a bill payment, the user types the destination amount, so we mirror
// that reading: destination → origin.

import type { PricingQuoteResponse } from '@/types';

export interface FxQuoteProps {
  quote: PricingQuoteResponse | null;
  /** Currency the user pays in (= API origin). */
  originCurrency: string;
  /** Currency the bill is in (= API destination, where the typed amount lives). */
  destinationCurrency: string;
}

function formatAmount(value: number, currency: string): string {
  const symbols: Record<string, string> = { GBP: '£', NGN: '₦', USD: '$', EUR: '€' };
  const prefix = symbols[currency] ?? `${currency} `;
  return `${prefix}${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function FxQuote({ quote, originCurrency, destinationCurrency }: FxQuoteProps) {
  if (!quote || !originCurrency || !destinationCurrency) return null;
  const sameCurrency = originCurrency.toUpperCase() === destinationCurrency.toUpperCase();
  if (sameCurrency) return null;

  // Display rate as "1 destination = X origin" so it matches the input-first
  // reading on the row above.
  const rate =
    quote.exchangeRate && quote.exchangeRate > 0
      ? 1 / quote.exchangeRate
      : quote.destinationAmount > 0
        ? quote.originAmount / quote.destinationAmount
        : 0;

  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-10)] px-3.5 py-2.5">
      <div className="text-[12px] font-medium text-[var(--color-brand-primary)]">Live FX quote</div>
      <div className="font-[family-name:var(--font-mono)] text-[13px] font-bold text-[var(--color-brand-primary)]">
        {formatAmount(quote.destinationAmount, destinationCurrency)} →{' '}
        {formatAmount(quote.originAmount, originCurrency)}
      </div>
      <div className="text-[11px] text-[var(--color-brand-primary)] opacity-75">
        1 {destinationCurrency} = {rate.toLocaleString(undefined, { maximumFractionDigits: 4 })}{' '}
        {originCurrency}
      </div>
    </div>
  );
}
