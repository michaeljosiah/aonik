import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { ArrowUpRight, Minus, TrendingDown, TrendingUp } from 'lucide-react';

import { Card, CardContent, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

type TrendDirection = 'up' | 'down' | 'neutral';

export type FinancialSnapshotTrend = {
  direction: TrendDirection;
  value: string;
  label: string;
};

export type FinancialSnapshotData = {
  id: string;
  title: string;
  description: string;
  value: string;
  valueLabel?: string;
  trend?: FinancialSnapshotTrend;
  footerLabel?: string;
  footerHref?: string;
  sparkline?: number[];
  accent?: string;
};

const trendIconMap = {
  up: TrendingUp,
  down: TrendingDown,
  neutral: Minus,
};

function TrendBadge({ trend }: { trend: FinancialSnapshotTrend }) {
  const Icon = trendIconMap[trend.direction];
  const colorClass =
    trend.direction === 'up'
      ? 'text-[var(--color-success)]'
      : trend.direction === 'down'
        ? 'text-[var(--color-danger)]'
        : 'text-[var(--color-text-tertiary)]';

  return (
    <div className={cn('inline-flex items-center gap-1 text-xs font-medium', colorClass)}>
      <Icon className="h-3.5 w-3.5" />
      {trend.value}
      <span className="text-[var(--color-text-tertiary)] font-normal">{trend.label}</span>
    </div>
  );
}

function MiniSparkline({ values, color }: { values: number[]; color?: string }) {
  const { path, area } = useMemo(() => {
    if (values.length === 0) return { path: '', area: '' };

    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    const divider = values.length - 1 || 1;
    const points = values.map((value, index) => {
      const x = (index / divider) * 100;
      const y = 30 - ((value - min) / range) * 24;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    });

    const pathData = `M ${points.join(' L ')}`;
    const areaData = `M 0,30 L ${points.join(' L ')} L 100,30 Z`;
    return { path: pathData, area: areaData };
  }, [values]);

  const strokeColor = color || 'rgba(99, 102, 241, 0.9)';
  const fillColor = color ? `${color}30` : 'rgba(99, 102, 241, 0.18)';

  return (
    <svg viewBox="0 0 100 30" className="h-10 w-28">
      <path d={area} fill={fillColor} />
      <path d={path} stroke={strokeColor} strokeWidth="2" fill="none" />
    </svg>
  );
}

export function FinancialSnapshotCard({ card }: { card: FinancialSnapshotData }) {
  return (
    <Card
      className="relative border-l-4"
      style={{ borderLeftColor: card.accent || 'var(--color-border-light)' }}
    >
      <CardContent className="p-5">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
              {card.title}
            </p>
            <p className="mt-1 text-sm text-[var(--color-text-secondary)]">{card.description}</p>
          </div>
          {card.trend && <TrendBadge trend={card.trend} />}
        </div>

        <div className="mt-4 flex items-end justify-between gap-4">
          <div>
            <div className="text-2xl font-semibold text-[var(--color-text-primary)]">
              {card.value}
            </div>
            {card.valueLabel && (
              <div className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                {card.valueLabel}
              </div>
            )}
          </div>
          {card.sparkline && card.sparkline.length > 0 && (
            <MiniSparkline values={card.sparkline} color={card.accent} />
          )}
        </div>
      </CardContent>

      {card.footerLabel && card.footerHref && (
        <CardFooter className="pt-0">
          <Button variant="ghost" size="sm" asChild className="gap-2">
            <Link to={card.footerHref}>
              {card.footerLabel}
              <ArrowUpRight className="h-4 w-4" />
            </Link>
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}
