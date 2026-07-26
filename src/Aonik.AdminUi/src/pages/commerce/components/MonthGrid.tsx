// MonthGrid (Spec 073 §5) — hand-built 7-column Monday-first CSS grid used by
// the delivery calendar (Spec 077). No calendar library exists in this repo
// and none should be added. Blackout cells strike the numeral; the promise
// cell rings with the brand primary.

import { cn } from '@/lib/utils';

import { MONTH_GRID_WEEKDAYS, buildMonthCells, type MonthGridDay } from './monthGridMath';

export interface MonthGridLegendEntry {
  kind: MonthGridDay['kind'];
  label: string;
}

interface MonthGridProps {
  monthLabel: string;
  /** 0 = Monday … 6 = Sunday — the column day 1 lands in. */
  firstWeekday: number;
  days: MonthGridDay[];
  legend?: MonthGridLegendEntry[];
}

function dayCellClasses(kind: MonthGridDay['kind']): string {
  switch (kind) {
    case 'delivery':
      return 'bg-[var(--color-brand-primary-10)] font-medium text-[var(--color-brand-primary)]';
    case 'blackout':
      return 'text-[var(--color-text-tertiary)] line-through';
    case 'promise':
      return 'font-semibold text-[var(--color-brand-primary)] ring-2 ring-inset ring-[var(--color-brand-primary)]';
    default:
      return 'text-[var(--color-text-secondary)]';
  }
}

function legendSwatchClasses(kind: MonthGridDay['kind']): string {
  switch (kind) {
    case 'delivery':
      return 'bg-[var(--color-brand-primary-10)]';
    case 'blackout':
      return 'border border-[var(--color-border)] bg-transparent';
    case 'promise':
      return 'ring-2 ring-inset ring-[var(--color-brand-primary)]';
    default:
      return 'bg-[var(--color-surface-inset)]';
  }
}

export function MonthGrid({ monthLabel, firstWeekday, days, legend }: MonthGridProps) {
  const cells = buildMonthCells(firstWeekday, days);

  return (
    <div>
      <div className="mb-2 text-[12.5px] font-semibold text-[var(--color-text-primary)]">
        {monthLabel}
      </div>
      <div className="grid grid-cols-7 gap-1">
        {MONTH_GRID_WEEKDAYS.map((weekday) => (
          <div
            key={weekday}
            className="pb-1 text-center text-[10px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]"
          >
            {weekday}
          </div>
        ))}
        {cells.map((cell, index) =>
          cell === null ? (
            <div key={`pad-${index}`} aria-hidden />
          ) : (
            <div
              key={cell.day}
              className={cn(
                'flex h-8 items-center justify-center rounded-md font-mono text-[12px] tabular-nums',
                dayCellClasses(cell.kind),
              )}
            >
              {cell.day}
            </div>
          ),
        )}
      </div>
      {legend && legend.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1.5">
          {legend.map((entry) => (
            <span
              key={entry.kind}
              className="flex items-center gap-1.5 text-[11px] text-[var(--color-text-secondary)]"
            >
              <span className={cn('h-3 w-3 shrink-0 rounded-sm', legendSwatchClasses(entry.kind))} />
              {entry.label}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
