// Card — generic surface primitive matching the template's `Card` from
// templates/aonik-admin-starterkit/kit/components.jsx. Twelve-pixel radius,
// 1px light border, optional header row with title + subtitle + action slot,
// configurable padding. Used across MySpace and other dashboard screens.

import type { ReactNode, CSSProperties } from 'react';
import { cn } from '@/lib/utils';

export interface CardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  action?: ReactNode;
  /** Padding in px, applied to header and body. Default 20. */
  padding?: number;
  className?: string;
  style?: CSSProperties;
  children?: ReactNode;
}

export function Card({ title, subtitle, action, padding = 20, className, style, children }: CardProps) {
  const showHeader = title != null || action != null;
  return (
    <div
      className={cn(
        'rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]',
        className,
      )}
      style={style}
    >
      {showHeader && (
        <div
          className="flex items-start justify-between gap-4"
          style={{ padding: `${padding}px ${padding}px 0` }}
        >
          <div className="min-w-0">
            {title != null && (
              <div className="text-[14px] font-semibold text-[var(--color-text-primary)]">
                {title}
              </div>
            )}
            {subtitle != null && (
              <div className="mt-0.5 text-[12px] text-[var(--color-text-secondary)]">
                {subtitle}
              </div>
            )}
          </div>
          {action != null && <div className="shrink-0">{action}</div>}
        </div>
      )}
      <div style={{ padding }}>{children}</div>
    </div>
  );
}
