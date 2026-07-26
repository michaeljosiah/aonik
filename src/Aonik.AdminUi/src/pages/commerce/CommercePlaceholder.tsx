// Foundation placeholder (Spec 073 §8) — every commerce route resolves to a
// real, on-pattern page from day one; each page spec (074–084) replaces its
// placeholder with the full surface. House skeleton per Spec 073 §6.

import { Card as AonikCard, PageHeader } from '@/components/layout/aonik';

interface CommercePlaceholderProps {
  title: string;
  subtitle: string;
  /** The page spec that implements this surface. */
  spec: string;
  summary: string;
}

export function CommercePlaceholder({ title, subtitle, spec, summary }: CommercePlaceholderProps) {
  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader eyebrow="Commerce" title={title} subtitle={subtitle} />
      <AonikCard>
        <div className="flex flex-col items-start gap-2 py-6">
          <span className="rounded-full bg-[var(--color-surface-inset)] px-2.5 py-0.5 font-mono text-[11px] font-semibold text-[var(--color-text-secondary)]">
            Spec {spec}
          </span>
          <p className="max-w-xl text-[13px] leading-relaxed text-[var(--color-text-secondary)]">
            {summary} This page ships with Spec {spec}; the module, navigation, data layer and
            shared components it builds on are already in place.
          </p>
        </div>
      </AonikCard>
    </div>
  );
}
