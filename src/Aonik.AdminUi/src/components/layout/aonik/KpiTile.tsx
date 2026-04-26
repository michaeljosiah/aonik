// KpiTile — dashboard KPI primitive, 1:1 port of the template's KPI.
// Twelve-pixel surface card with label + optional delta pill + monospace
// value + optional sparkline. Sparkline takes a number[] (matches the
// MySpaceSummaryResponse FinancialMetricDto.sparkline shape) and renders
// a polyline + filled gradient polygon.

import { useId } from 'react';
import { ArrowDown, ArrowUp } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface KpiTileProps {
  label: string;
  value: string;
  /** Optional delta string (e.g. "+9.1%", "2 overdue"). */
  delta?: string;
  deltaTone?: 'up' | 'down' | 'neutral';
  /** Sparkline data normalised by the backend; rendered into a 100x30 viewBox. */
  sparkline?: number[];
  sparkColor?: string;
  className?: string;
}

function sparkPoints(data: number[]): string {
  if (!data.length) return '';
  // Render data into the 0..100 (x) × 0..30 (y, inverted) viewBox.
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  return data
    .map((v, i) => {
      const x = (i / Math.max(1, data.length - 1)) * 100;
      // Map larger values to smaller y so the line rises with the metric.
      const y = 30 - ((v - min) / range) * 26 - 2;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}

export function KpiTile({
  label,
  value,
  delta,
  deltaTone = 'up',
  sparkline,
  sparkColor = 'var(--color-brand-primary)',
  className,
}: KpiTileProps) {
  const gradientId = useId();
  const points = sparkline ? sparkPoints(sparkline) : '';
  const deltaIsDown = deltaTone === 'down';
  const deltaIsNeutral = deltaTone === 'neutral';
  const showArrow = !deltaIsNeutral;

  return (
    <div
      className={cn(
        'flex flex-col gap-3.5 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="text-[13px] font-medium text-[var(--color-text-secondary)]">{label}</div>
        {delta && (
          <span
            className="inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold"
            style={{
              background: deltaIsDown
                ? 'var(--color-error-light)'
                : deltaIsNeutral
                  ? 'var(--color-surface-inset)'
                  : 'var(--color-success-light)',
              color: deltaIsDown
                ? 'var(--color-error)'
                : deltaIsNeutral
                  ? 'var(--color-text-secondary)'
                  : 'var(--color-success)',
            }}
          >
            {showArrow &&
              (deltaIsDown ? <ArrowDown className="h-2.5 w-2.5" /> : <ArrowUp className="h-2.5 w-2.5" />)}
            {delta}
          </span>
        )}
      </div>

      <div
        className="text-[26px] font-semibold leading-none text-[var(--color-text-primary)]"
        style={{ fontFamily: 'var(--font-mono)', letterSpacing: '-0.01em' }}
      >
        {value}
      </div>

      {points && (
        <svg
          viewBox="0 0 100 30"
          preserveAspectRatio="none"
          className="block h-8 w-full"
          aria-hidden
        >
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={sparkColor} stopOpacity="0.25" />
              <stop offset="100%" stopColor={sparkColor} stopOpacity="0" />
            </linearGradient>
          </defs>
          <polygon fill={`url(#${gradientId})`} points={`0,30 ${points} 100,30`} />
          <polyline fill="none" stroke={sparkColor} strokeWidth="1.5" points={points} />
        </svg>
      )}
    </div>
  );
}
