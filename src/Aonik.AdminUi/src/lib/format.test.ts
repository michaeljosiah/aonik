import { afterEach, describe, expect, it, vi } from 'vitest';

import { formatCalendarDate, formatCurrency, formatDate } from './format';

describe('formatCurrency', () => {
  it('preserves minor units — a rounded total reports money the order does not have', () => {
    // Regression: maximumFractionDigits: 0 rendered GBP 95.50 as "£96". Commerce totals
    // carry real fractional prices, discounts and tax.
    expect(formatCurrency(95.5, 'GBP')).toContain('95.50');
    expect(formatCurrency(95.5, 'GBP')).not.toContain('96');
    expect(formatCurrency(0.99, 'USD')).toContain('0.99');
  });

  it('still omits decimals for zero-decimal currencies', () => {
    // Intl knows JPY has no minor unit, so the default is right for both cases.
    expect(formatCurrency(96, 'JPY')).not.toContain('.00');
  });

  it('formats whole amounts without inventing precision problems', () => {
    expect(formatCurrency(155, 'GBP')).toContain('155');
  });

  it('falls back for a malformed code instead of throwing', () => {
    expect(formatCurrency(95.5, 'NOTREAL')).toBe('NOTREAL 95.50');
  });
});

describe('formatDate', () => {
  it('renders an em dash for a missing date rather than "Invalid Date"', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('')).toBe('—');
  });

  it('formats an ISO timestamp', () => {
    expect(formatDate('2026-07-27T12:00:00Z')).toMatch(/2026/);
  });
});

describe('formatCalendarDate', () => {
  afterEach(() => vi.unstubAllEnvs());

  it.each(['America/Los_Angeles', 'Pacific/Kiritimati', 'Europe/London'])(
    'keeps the selected day for a viewer in %s', (timezone) => {
      vi.stubEnv('TZ', timezone);
      const expected = new Intl.DateTimeFormat(undefined, {
        year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC',
      }).format(new Date('2026-10-25T00:00:00Z'));
      expect(formatCalendarDate('2026-10-25')).toBe(expected);
    },
  );

  it.each([null, undefined, '', '2026-02-30', '2026-13-01', '2026-07-27T12:00:00Z'])(
    'withholds absent or invalid calendar date %s', (value) => {
      expect(formatCalendarDate(value)).toBe('—');
    },
  );
});
