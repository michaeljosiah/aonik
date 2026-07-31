import { describe, expect, it } from 'vitest';

import {
  CUSTOMER_DOMAIN_BILLING,
  CUSTOMER_DOMAIN_PERSONAL_FINANCE,
  CUSTOMER_DOMAIN_STOREFRONT,
  orderDomains,
  presentDomain,
} from './domainPresentation';

describe('presentDomain', () => {
  it('labels the known product lines', () => {
    expect(presentDomain(CUSTOMER_DOMAIN_STOREFRONT).label).toBe('Storefront');
    expect(presentDomain(CUSTOMER_DOMAIN_BILLING).label).toBe('Billing');
    expect(presentDomain(CUSTOMER_DOMAIN_PERSONAL_FINANCE).label).toBe('Payabo');
  });

  it('gives each known domain its own tone so chips are distinguishable', () => {
    const tones = [
      presentDomain(CUSTOMER_DOMAIN_STOREFRONT).tone,
      presentDomain(CUSTOMER_DOMAIN_BILLING).tone,
      presentDomain(CUSTOMER_DOMAIN_PERSONAL_FINANCE).tone,
    ];
    expect(new Set(tones).size).toBe(3);
  });

  it('still renders an UNKNOWN domain — a new module must not vanish from the registry', () => {
    // A module can ship a product line without a frontend release; dropping its chip would
    // under-report a customer who genuinely participates in it.
    expect(presentDomain('loyalty-club')).toEqual({ label: 'Loyalty club', tone: 'default' });
    expect(presentDomain('lending')).toEqual({ label: 'Lending', tone: 'default' });
  });

  it('does not crash on a blank key', () => {
    expect(presentDomain('').label).toBe('');
    expect(presentDomain('   ').tone).toBe('default');
  });
});

describe('orderDomains', () => {
  it('orders known domains consistently regardless of server order', () => {
    const expected = [
      CUSTOMER_DOMAIN_STOREFRONT,
      CUSTOMER_DOMAIN_BILLING,
      CUSTOMER_DOMAIN_PERSONAL_FINANCE,
    ];
    expect(orderDomains([...expected].reverse())).toEqual(expected);
    expect(orderDomains([CUSTOMER_DOMAIN_BILLING, CUSTOMER_DOMAIN_STOREFRONT])).toEqual([
      CUSTOMER_DOMAIN_STOREFRONT,
      CUSTOMER_DOMAIN_BILLING,
    ]);
  });

  it('places unknown domains after known ones, alphabetically', () => {
    expect(orderDomains(['zeta', CUSTOMER_DOMAIN_BILLING, 'alpha'])).toEqual([
      CUSTOMER_DOMAIN_BILLING,
      'alpha',
      'zeta',
    ]);
  });

  it('de-duplicates', () => {
    expect(orderDomains([CUSTOMER_DOMAIN_BILLING, CUSTOMER_DOMAIN_BILLING])).toEqual([
      CUSTOMER_DOMAIN_BILLING,
    ]);
  });

  it('handles the empty case', () => {
    expect(orderDomains([])).toEqual([]);
  });
});
