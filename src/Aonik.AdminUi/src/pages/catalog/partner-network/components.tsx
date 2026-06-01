// Partner Network hub — shared presentational primitives.
//
// These mirror the prototype's ViewToggle / ServiceChip / empty-state visuals
// (Templates/aonik-admin-starterkit/screens/partner-hub.jsx) but are ported to
// the real app's Tailwind + `--color-*` token system. Nothing here invents
// telemetry; the "honest gap" InfoNote is the surface we use wherever Spec 031
// has no backing endpoint yet.

import type { LucideIcon } from 'lucide-react';
import { Info, LayoutGrid, List } from 'lucide-react';
import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export type HubView = 'grid' | 'list';

// ─── ViewToggle ──────────────────────────────────────────────────────────────

export interface ViewToggleProps {
  view: HubView;
  onChange: (view: HubView) => void;
  className?: string;
}

const VIEW_OPTIONS: { value: HubView; icon: LucideIcon; label: string }[] = [
  { value: 'grid', icon: LayoutGrid, label: 'Grid' },
  { value: 'list', icon: List, label: 'List' },
];

/** Grid/list switch shown on the right of a tab toolbar. */
export function ViewToggle({ view, onChange, className }: ViewToggleProps) {
  return (
    <div className={cn('flex items-center gap-1.5', className)}>
      <span className="mr-1 text-[11px] text-[var(--color-text-tertiary)]">View</span>
      {VIEW_OPTIONS.map(({ value, icon: Icon, label }) => {
        const active = view === value;
        return (
          <button
            key={value}
            type="button"
            onClick={() => onChange(value)}
            aria-pressed={active}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-[11.5px] font-medium transition-colors',
              active
                ? 'border-[var(--color-border)] bg-[var(--color-surface-inset)] text-[var(--color-text-primary)]'
                : 'border-[var(--color-border-light)] text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]',
            )}
          >
            <Icon size={12} />
            {label}
          </button>
        );
      })}
    </div>
  );
}

// ─── Chip ────────────────────────────────────────────────────────────────────

export interface ChipProps {
  icon?: LucideIcon;
  dense?: boolean;
  className?: string;
  children: ReactNode;
}

/**
 * Generic neutral badge for country / currency / method tokens. Distinct from
 * `Pill` (which carries semantic status tone) — this is the inert "tag" treatment
 * the prototype's ServiceChip uses.
 */
export function Chip({ icon: Icon, dense = false, className, children }: ChipProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] font-medium text-[var(--color-text-secondary)]',
        dense ? 'px-1.5 py-0.5 text-[10px]' : 'px-2 py-[3px] text-[11px]',
        className,
      )}
    >
      {Icon && <Icon size={dense ? 9 : 10} className="text-[var(--color-text-tertiary)]" />}
      {children}
    </span>
  );
}

// ─── InfoNote ────────────────────────────────────────────────────────────────

export interface InfoNoteProps {
  icon?: LucideIcon;
  children: ReactNode;
  className?: string;
}

/**
 * Small dashed advisory used to be honest about Spec 031 gaps — e.g. "lane-based
 * routing selection is not yet wired" or "showing first N of M". Deliberately
 * low-contrast so it reads as a footnote, not an error.
 */
export function InfoNote({ icon: Icon = Info, children, className }: InfoNoteProps) {
  return (
    <div
      className={cn(
        'flex items-start gap-2 rounded-lg border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2 text-[11.5px] leading-relaxed text-[var(--color-text-tertiary)]',
        className,
      )}
    >
      <Icon size={13} className="mt-px flex-none text-[var(--color-text-tertiary)]" />
      <span>{children}</span>
    </div>
  );
}

// ─── Panel ───────────────────────────────────────────────────────────────────

export interface PanelProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  action?: ReactNode;
  className?: string;
  bodyClassName?: string;
  children: ReactNode;
}

/**
 * Surface with a bordered, padded header and a *flush* body — the variant the
 * `Card` primitive can't express (its single `padding` value applies to both
 * header and body). Used for the divided list panels (Network health, Routing
 * rules, Activity) where rows should run edge-to-edge under a titled header.
 */
export function Panel({ title, subtitle, action, className, bodyClassName, children }: PanelProps) {
  const showHeader = title != null || action != null;
  return (
    <div
      className={cn(
        'overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]',
        className,
      )}
    >
      {showHeader && (
        <div className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] px-5 py-4">
          <div className="min-w-0">
            {title != null && (
              <div className="text-[14px] font-semibold text-[var(--color-text-primary)]">{title}</div>
            )}
            {subtitle != null && (
              <div className="mt-0.5 text-[12px] text-[var(--color-text-secondary)]">{subtitle}</div>
            )}
          </div>
          {action != null && <div className="shrink-0">{action}</div>}
        </div>
      )}
      <div className={bodyClassName}>{children}</div>
    </div>
  );
}

// ─── EmptyState ──────────────────────────────────────────────────────────────

export interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}

/**
 * Centered placeholder for "no data" and "awaiting backend" surfaces. The
 * Updates tab uses this as its primary content because there is no webhook-inbox
 * endpoint (Spec 031 gap C4) — the description states that plainly rather than
 * faking an event stream.
 */
export function EmptyState({ icon: Icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface)] px-6 py-14 text-center',
        className,
      )}
    >
      <div className="flex h-11 w-11 items-center justify-center rounded-full bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]">
        <Icon size={20} />
      </div>
      <div className="w-full max-w-md space-y-1">
        <p className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</p>
        {description && (
          <p className="text-[12.5px] leading-relaxed text-[var(--color-text-tertiary)]">{description}</p>
        )}
      </div>
      {action}
    </div>
  );
}
