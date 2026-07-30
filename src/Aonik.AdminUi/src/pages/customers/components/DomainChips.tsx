// DomainChips (Spec 080) — the product lines one customer participates in, shown on the
// unified registry row. A tenant selling merchandise and financial services has ONE customer
// base, so these chips are the lens, never a reason to split the registry into separate views.

import { Pill } from '@/components/layout/aonik';

import { orderDomains, presentDomain } from './domainPresentation';

interface DomainChipsProps {
  domains: string[];
}

export function DomainChips({ domains }: DomainChipsProps) {
  const ordered = orderDomains(domains);

  // No domains is a real state — the customer exists but has transacted nowhere yet — and it
  // reads as an em dash rather than an empty cell, so it cannot be mistaken for missing data.
  if (ordered.length === 0) {
    return <span className="text-[var(--color-text-tertiary)]">—</span>;
  }

  return (
    <span className="flex flex-wrap items-center gap-1">
      {ordered.map((domain) => {
        const { label, tone } = presentDomain(domain);
        return (
          <Pill key={domain} tone={tone} size="sm">
            {label}
          </Pill>
        );
      })}
    </span>
  );
}
