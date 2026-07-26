// Pure scale/tick derivation behind <PriceCurve/> (Spec 073 §5) — node-testable.

export interface PriceCurvePreset {
  size: number;
  price: number;
}

/** Every integer size in [min, max]. Degenerate ranges collapse to [min]. */
export function curveSizes(min: number, max: number): number[] {
  if (max < min) return [min];
  return Array.from({ length: max - min + 1 }, (_, i) => min + i);
}

/**
 * The y-domain: the lowest/highest price either line or any preset reaches
 * across the full size range, padded 8% each side so dots never sit on the
 * frame. A flat domain (single price everywhere) widens by ±1 to stay drawable.
 */
export function priceBounds(
  sizes: number[],
  formula: (size: number) => number,
  effective: (size: number) => number,
  presets: PriceCurvePreset[],
): { lo: number; hi: number } {
  const values = [
    ...sizes.map(formula),
    ...sizes.map(effective),
    ...presets.map((p) => p.price),
  ];
  let lo = Math.min(...values);
  let hi = Math.max(...values);
  if (hi === lo) {
    lo -= 1;
    hi += 1;
  }
  const pad = (hi - lo) * 0.08;
  return { lo: lo - pad, hi: hi + pad };
}

/**
 * X ticks thinned for readability: every 4th size from min, plus every preset
 * size, plus max — deduplicated and sorted (Spec 073 §5).
 */
export function buildXTicks(min: number, max: number, presetSizes: number[]): number[] {
  const ticks = new Set<number>();
  for (let size = min; size <= max; size += 4) ticks.add(size);
  ticks.add(max);
  for (const size of presetSizes) {
    if (size >= min && size <= max) ticks.add(size);
  }
  return Array.from(ticks).sort((a, b) => a - b);
}

/**
 * ~`count` round-value y ticks inside [lo, hi] on a 1/2/5 ladder, choosing the
 * step by the standard √2/√10/√50 rounding thresholds (the d3 tick rule) so a
 * raw step of e.g. 28 rounds to 20 rather than jumping to 50 and starving the
 * axis of gridlines.
 */
export function buildYTicks(lo: number, hi: number, count = 4): number[] {
  const span = hi - lo;
  if (span <= 0) return [lo];
  const rawStep = span / count;
  const magnitude = Math.pow(10, Math.floor(Math.log10(rawStep)));
  const error = rawStep / magnitude; // ∈ [1, 10)
  const step =
    error >= Math.sqrt(50) ? 10 * magnitude
    : error >= Math.sqrt(10) ? 5 * magnitude
    : error >= Math.sqrt(2) ? 2 * magnitude
    : magnitude;
  const first = Math.ceil(lo / step) * step;
  const ticks: number[] = [];
  // Float-tolerant upper bound so `hi` itself survives accumulation error.
  for (let v = first; v <= hi + step * 1e-6; v += step) {
    ticks.push(Number(v.toFixed(10)));
  }
  return ticks;
}

export interface CurveScales {
  x: (size: number) => number;
  y: (price: number) => number;
}

/** Linear pixel scales over the size range and price bounds, inset by `pad`. */
export function makeScales(options: {
  min: number;
  max: number;
  lo: number;
  hi: number;
  width: number;
  height: number;
  pad: { left: number; right: number; top: number; bottom: number };
}): CurveScales {
  const { min, max, lo, hi, width, height, pad } = options;
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;
  const sizeSpan = Math.max(1, max - min);
  const priceSpan = Math.max(1e-9, hi - lo);
  return {
    x: (size) => pad.left + ((size - min) / sizeSpan) * innerW,
    y: (price) => pad.top + (1 - (price - lo) / priceSpan) * innerH,
  };
}
