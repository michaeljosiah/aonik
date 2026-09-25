import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';

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
  /** Leading status dot. Signifies state, so it stays (no decorative dots). */
  dot?: boolean;
  /** Kept for compatibility; both sizes render as the standard badge. */
  size?: 'sm' | 'md';
  className?: string;
  children: ReactNode;
}

const toneVariant = {
  default: 'secondary',
  muted: 'outline',
  success: 'success',
  warning: 'warning',
  danger: 'destructive-subtle',
  info: 'info',
  pending: 'warning',
} as const;

/**
 * Status pill, now a Badge (Spec 098 D7). `danger` uses a subtle destructive
 * tint rather than the solid badge so status pills read at one weight.
 */
export function Pill({ tone = 'default', dot = false, className, children }: PillProps) {
  const variant = toneVariant[tone];
  return (
    <Badge
      variant={variant === 'destructive-subtle' ? 'outline' : variant}
      className={cn(
        variant === 'destructive-subtle' &&
          'border-transparent bg-destructive/10 text-destructive dark:bg-destructive/20',
        tone === 'muted' && 'text-muted-foreground',
        className,
      )}
    >
      {dot && <span aria-hidden="true" className="size-1.5 shrink-0 rounded-full bg-current" />}
      {children}
    </Badge>
  );
}
