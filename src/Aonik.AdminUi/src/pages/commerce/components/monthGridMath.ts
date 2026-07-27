// Pure cell layout behind <MonthGrid/> (Spec 073 §5) — node-testable.

export type MonthDayKind = 'plain' | 'delivery' | 'blackout' | 'promise';

export interface MonthGridDay {
  day: number;
  kind: MonthDayKind;
}

/**
 * Lays a month's days onto a Monday-first 7-column grid: `firstWeekday`
 * (0 = Monday … 6 = Sunday — the column day 1 lands in) leading `null`
 * placeholders, then the days in order. Out-of-range weekday values clamp
 * into 0..6 so a bad payload skews the layout instead of crashing it.
 */
export function buildMonthCells(
  firstWeekday: number,
  days: MonthGridDay[],
): Array<MonthGridDay | null> {
  const lead = Math.min(6, Math.max(0, Math.trunc(firstWeekday)));
  return [...Array.from({ length: lead }, () => null), ...days];
}

/** Monday-first column headings, matching the `firstWeekday` convention. */
export const MONTH_GRID_WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] as const;
