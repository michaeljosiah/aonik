// SignedAmount (Spec 073 §5) — the single rendering of a signed price delta
// used across Specs 074/075/076/082: mono, tabular-nums, `included` for zero.

import { cn } from '@/lib/utils';

import { formatSignedAmount } from './signedAmountFormat';

interface SignedAmountProps {
  amount: number;
  currency: string;
  className?: string;
}

export function SignedAmount({ amount, currency, className }: SignedAmountProps) {
  const isZero = amount === 0;
  return (
    <span
      className={cn(
        'font-mono text-[12.5px] tabular-nums',
        isZero
          ? 'text-[var(--color-text-tertiary)]'
          : amount > 0
            ? 'text-[var(--color-text-primary)]'
            : 'text-[var(--color-success)]',
        className,
      )}
    >
      {formatSignedAmount(amount, currency)}
    </span>
  );
}
