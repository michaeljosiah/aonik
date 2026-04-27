import type { ReactNode } from 'react';

export interface PageHeaderProps {
  eyebrow?: ReactNode;
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
}

/**
 * Page header — eyebrow / H1 / subtitle on the left, optional action row on
 * the right. Uses the brand font for the title and the `.eyebrow` utility
 * for the small uppercase label, matching the template's layout.
 */
export function PageHeader({ eyebrow, title, subtitle, actions }: PageHeaderProps) {
  return (
    <div className="flex items-end justify-between gap-6">
      <div className="min-w-0">
        {eyebrow && <div className="eyebrow">{eyebrow}</div>}
        <h1
          className="font-[family-name:var(--font-brand)] text-2xl font-bold tracking-[-0.01em] text-[var(--color-text-primary)]"
          style={{ marginTop: eyebrow ? 6 : 0 }}
        >
          {title}
        </h1>
        {subtitle && (
          <div className="mt-1 text-[13px] text-[var(--color-text-secondary)]">{subtitle}</div>
        )}
      </div>
      {actions && <div className="flex flex-none items-center gap-2">{actions}</div>}
    </div>
  );
}
