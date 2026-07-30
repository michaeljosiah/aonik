// Pure presentation mapping behind <DomainChips/> (Spec 080) — kept free of React
// so the node-environment vitest suite can exercise it directly.

import type { PillTone } from '@/components/layout/aonik';

/** Domain keys the server ships today; the list is open by design. */
export const CUSTOMER_DOMAIN_BILLING = 'billing';
export const CUSTOMER_DOMAIN_STOREFRONT = 'storefront';
export const CUSTOMER_DOMAIN_PERSONAL_FINANCE = 'personal-finance';

interface DomainPresentation {
  label: string;
  tone: PillTone;
}

const KNOWN: Record<string, DomainPresentation> = {
  [CUSTOMER_DOMAIN_BILLING]: { label: 'Billing', tone: 'muted' },
  [CUSTOMER_DOMAIN_STOREFRONT]: { label: 'Storefront', tone: 'info' },
  [CUSTOMER_DOMAIN_PERSONAL_FINANCE]: { label: 'Payabo', tone: 'pending' },
};

/**
 * How one domain key renders. An UNKNOWN key still renders — a module can ship a new
 * product line without a frontend release, and showing its raw key is far better than
 * silently dropping a domain the customer genuinely participates in. Kebab-case is
 * humanised so `loyalty-club` reads as "Loyalty club" rather than shouting the key.
 */
export function presentDomain(domain: string): DomainPresentation {
  const known = KNOWN[domain];
  if (known) return known;

  const trimmed = domain.trim();
  if (!trimmed) return { label: domain, tone: 'default' };
  const humanised = trimmed.replace(/[-_]+/g, ' ');
  return {
    label: humanised.charAt(0).toUpperCase() + humanised.slice(1),
    tone: 'default',
  };
}

/** Stable ordering: known domains in a fixed order first, then unknown ones alphabetically. */
export function orderDomains(domains: readonly string[]): string[] {
  const rank = [CUSTOMER_DOMAIN_STOREFRONT, CUSTOMER_DOMAIN_BILLING, CUSTOMER_DOMAIN_PERSONAL_FINANCE];
  return [...new Set(domains)].sort((a, b) => {
    const ra = rank.indexOf(a);
    const rb = rank.indexOf(b);
    if (ra !== -1 && rb !== -1) return ra - rb;
    if (ra !== -1) return -1;
    if (rb !== -1) return 1;
    return a.localeCompare(b);
  });
}
