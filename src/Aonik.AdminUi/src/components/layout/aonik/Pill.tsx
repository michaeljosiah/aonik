import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export type PillTone =
  | 'default'
  | 'muted'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'pending';

export interface PillProps {
  tone?: PillTone;
  dot?: boolean;
  size?: 'sm' | 'md';
  className?: string;
  children: ReactNode;
}

const toneClasses: Record<PillTone, string> = {
  default:
    'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] border-[var(--color-border-light)]',
  muted:
    'bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)] border-[var(--color-border-light)]',
  success:
    'bg-[var(--color-success-light)] text-[var(--color-success)] border-transparent',
  warning:
    'bg-[var(--color-warning-light)] text-[var(--color-warning)] border-transparent',
  danger:
    'bg-[var(--color-danger-10)] text-[var(--color-danger)] border-transparent',
  info:
    'bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)] border-transparent',
  pending:
    'bg-[var(--color-brand-secondary-10)] text-[var(--color-brand-secondary)] border-transparent',
};

/**
 * Status pill with semantic tones. Use `dot` to render a small circle in the
 * pill colour before the label — the template uses this for KYC and order
 * status cells to make the row scannable at a glance.
 */
export function Pill({ tone = 'default', dot = false, size = 'sm', className, children }: PillProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border font-medium',
        size === 'sm' ? 'h-5 px-2 text-[10px]' : 'h-6 px-2.5 text-[11px]',
        toneClasses[tone],
        className,
      )}
    >
      {dot && (
        <span
          className="rounded-full"
          style={{
            width: size === 'sm' ? 5 : 6,
            height: size === 'sm' ? 5 : 6,
            background: 'currentColor',
            flex: 'none',
          }}
        />
      )}
      {children}
    </span>
  );
}
