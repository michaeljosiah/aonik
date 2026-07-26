import { describe, expect, it } from 'vitest';

import { buildXTicks, buildYTicks, curveSizes, makeScales, priceBounds } from './priceCurveMath';

describe('curveSizes', () => {
  it('yields every integer size across the range, inclusive', () => {
    expect(curveSizes(4, 8)).toEqual([4, 5, 6, 7, 8]);
  });

  it('collapses a degenerate range to the minimum', () => {
    expect(curveSizes(6, 6)).toEqual([6]);
    expect(curveSizes(6, 2)).toEqual([6]);
  });
});

describe('priceBounds', () => {
  const formula = (s: number) => 60 + (s - 4) * 12; // 60 @4 … 156 @12
  const effective = (s: number) => (s === 8 ? 100 : formula(s)); // preset undercuts @8

  it('spans the lowest and highest values either line or any preset reaches, padded', () => {
    const sizes = curveSizes(4, 12);
    const { lo, hi } = priceBounds(sizes, formula, effective, [{ size: 8, price: 100 }]);
    expect(lo).toBeLessThan(60);
    expect(hi).toBeGreaterThan(156);
    // 8% padding each side of the raw 60..156 span.
    expect(lo).toBeCloseTo(60 - 96 * 0.08, 6);
    expect(hi).toBeCloseTo(156 + 96 * 0.08, 6);
  });

  it('widens a flat domain so it stays drawable', () => {
    const { lo, hi } = priceBounds([4, 5], () => 50, () => 50, []);
    expect(hi).toBeGreaterThan(lo);
  });
});

describe('buildXTicks', () => {
  it('thins to every 4th size plus every preset size plus max, deduplicated and sorted', () => {
    // From 4: 4, 8, 12, 16 … plus max 14, plus presets 6 and 8 (8 dedupes).
    expect(buildXTicks(4, 14, [6, 8])).toEqual([4, 6, 8, 12, 14]);
  });

  it('ignores preset sizes outside the range', () => {
    expect(buildXTicks(4, 8, [2, 20])).toEqual([4, 8]);
  });
});

describe('buildYTicks', () => {
  it('produces round-valued ticks inside the domain', () => {
    const ticks = buildYTicks(52, 164);
    expect(ticks.length).toBeGreaterThanOrEqual(3);
    expect(ticks[0]).toBeGreaterThanOrEqual(52);
    expect(ticks[ticks.length - 1]).toBeLessThanOrEqual(164);
    // 1/2/5 ladder means a clean step, so consecutive gaps are equal.
    const gaps = ticks.slice(1).map((t, i) => t - ticks[i]);
    expect(new Set(gaps.map((g) => g.toFixed(6))).size).toBe(1);
  });

  it('degrades to the lower bound for an empty span', () => {
    expect(buildYTicks(10, 10)).toEqual([10]);
  });
});

describe('makeScales', () => {
  const scales = makeScales({
    min: 4,
    max: 12,
    lo: 50,
    hi: 170,
    width: 640,
    height: 240,
    pad: { left: 56, right: 16, top: 16, bottom: 30 },
  });

  it('pins the size range to the padded x extent', () => {
    expect(scales.x(4)).toBe(56);
    expect(scales.x(12)).toBe(640 - 16);
  });

  it('inverts y so higher prices sit higher on the chart', () => {
    expect(scales.y(50)).toBe(240 - 30); // lo at the bottom
    expect(scales.y(170)).toBe(16); // hi at the top
    expect(scales.y(170)).toBeLessThan(scales.y(50));
  });
});
