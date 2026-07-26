import { describe, expect, it } from 'vitest';

import { formatSignedAmount, formatUnsignedAmount } from './signedAmountFormat';

describe('formatSignedAmount', () => {
  it('prefixes positive deltas with +', () => {
    expect(formatSignedAmount(2.5, 'GBP')).toBe('+£2.50');
  });

  it('renders negative deltas with a true minus sign (U+2212)', () => {
    expect(formatSignedAmount(-2, 'GBP')).toBe('−£2.00');
    expect(formatSignedAmount(-2, 'GBP')).not.toContain('-'); // never the ASCII hyphen
  });

  it('renders exactly zero as "included"', () => {
    expect(formatSignedAmount(0, 'GBP')).toBe('included');
  });

  it('honours the currency the DTO carries — it never converts or assumes', () => {
    expect(formatSignedAmount(10, 'EUR')).toBe('+€10.00');
  });

  it('falls back to CODE-prefixed formatting for an unknown currency code', () => {
    expect(formatSignedAmount(3.5, 'NOTREAL')).toBe('+NOTREAL 3.50');
  });
});

describe('formatUnsignedAmount', () => {
  it('formats plain money in the given currency', () => {
    expect(formatUnsignedAmount(95, 'GBP')).toBe('£95.00');
  });

  it('falls back for malformed codes instead of throwing', () => {
    // Well-formed unknown codes (e.g. "XXQ") Intl formats itself; only a
    // malformed code takes the fallback path.
    expect(formatUnsignedAmount(1.239, 'NOTREAL')).toBe('NOTREAL 1.24');
  });
});
