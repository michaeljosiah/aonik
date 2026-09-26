import type { ReactNode, CSSProperties } from 'react';
import { cn } from '@/lib/utils';
import {
  Card as UiCard,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

export interface CardProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  action?: ReactNode;
  /** Body padding in px. Omit for the standard card padding (24px). */
  padding?: number;
  className?: string;
  style?: CSSProperties;
  children?: ReactNode;
}

/**
 * Props-driven convenience over ui/card (Spec 098 D7): title, subtitle and
 * action map onto CardHeader / CardTitle / CardDescription / CardAction.
 * New code can compose the ui/card parts directly.
 */
export function Card({ title, subtitle, action, padding, className, style, children }: CardProps) {
  const showHeader = title != null || action != null;
  const custom = padding !== undefined;

  return (
    <UiCard className={className} style={style}>
      {showHeader && (
        <CardHeader
          className={cn(custom && 'gap-1 pb-0')}
          style={custom ? { padding: `${Math.max(padding, 12)}px ${Math.max(padding, 12)}px 0` } : undefined}
        >
          {title != null && <CardTitle className="text-sm">{title}</CardTitle>}
          {subtitle != null && <CardDescription className="text-xs">{subtitle}</CardDescription>}
          {action != null && <CardAction>{action}</CardAction>}
        </CardHeader>
      )}
      <CardContent
        className={cn(!showHeader && !custom && 'pt-6', showHeader && !custom && 'pt-4')}
        style={custom ? { padding } : undefined}
      >
        {children}
      </CardContent>
    </UiCard>
  );
}
