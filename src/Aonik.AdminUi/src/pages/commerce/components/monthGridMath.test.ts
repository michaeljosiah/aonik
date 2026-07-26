import { describe, expect, it } from 'vitest';

import { buildMonthCells, type MonthGridDay } from './monthGridMath';

const days = (count: number): MonthGridDay[] =>
  Array.from({ length: count }, (_, i) => ({ day: i + 1, kind: 'plain' as const }));

describe('buildMonthCells', () => {
  it('inserts firstWeekday leading placeholders so day 1 lands in the right column', () => {
    // A month whose 1st is a Wednesday (Monday-first column 2).
    const cells = buildMonthCells(2, days(30));
    expect(cells.slice(0, 2)).toEqual([null, null]);
    expect(cells[2]).toEqual({ day: 1, kind: 'plain' });
    expect(cells).toHaveLength(32);
  });

  it('adds no placeholders when the month starts on Monday', () => {
    const cells = buildMonthCells(0, days(31));
    expect(cells[0]).toEqual({ day: 1, kind: 'plain' });
    expect(cells).toHaveLength(31);
  });

  it('handles a Sunday start (column 6)', () => {
    const cells = buildMonthCells(6, days(28));
    expect(cells.filter((c) => c === null)).toHaveLength(6);
    expect(cells[6]).toEqual({ day: 1, kind: 'plain' });
  });

  it('clamps out-of-range weekday values instead of crashing the layout', () => {
    expect(buildMonthCells(9, days(3)).filter((c) => c === null)).toHaveLength(6);
    expect(buildMonthCells(-3, days(3)).filter((c) => c === null)).toHaveLength(0);
  });

  it('preserves day kinds in order after the placeholders', () => {
    const input: MonthGridDay[] = [
      { day: 1, kind: 'plain' },
      { day: 2, kind: 'delivery' },
      { day: 3, kind: 'blackout' },
      { day: 4, kind: 'promise' },
    ];
    const cells = buildMonthCells(1, input);
    expect(cells.slice(1)).toEqual(input);
  });
});
