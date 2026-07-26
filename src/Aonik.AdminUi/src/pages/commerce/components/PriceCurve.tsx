// PriceCurve (Spec 073 §5) — hand-rolled SVG rendering of a size-tiered
// bundle's pricing (Spec 076): dashed formula line, solid effective line
// (presets win at their size), preset dots with authored-saving annotations.
// The KpiTile sparkline precedent, not recharts — the annotations and dual
// lines fight a charting library.

import { formatUnsignedAmount } from './signedAmountFormat';
import {
  buildXTicks,
  buildYTicks,
  curveSizes,
  makeScales,
  priceBounds,
  type PriceCurvePreset,
} from './priceCurveMath';

interface PriceCurveProps {
  min: number;
  max: number;
  /** BasePrice + (size − baseSize) × perSpacePrice — the un-overridden line. */
  formula: (size: number) => number;
  /** The price actually charged at each size (presets win at their size). */
  effective: (size: number) => number;
  presets: PriceCurvePreset[];
  currency: string;
}

const WIDTH = 640;
const HEIGHT = 240;
const PAD = { left: 56, right: 16, top: 16, bottom: 30 };

function linePath(sizes: number[], scale: (s: number) => number, yOf: (s: number) => number): string {
  return sizes
    .map((size, i) => `${i === 0 ? 'M' : 'L'}${scale(size).toFixed(1)},${yOf(size).toFixed(1)}`)
    .join(' ');
}

export function PriceCurve({ min, max, formula, effective, presets, currency }: PriceCurveProps) {
  const sizes = curveSizes(min, max);
  const { lo, hi } = priceBounds(sizes, formula, effective, presets);
  const scales = makeScales({ min, max, lo, hi, width: WIDTH, height: HEIGHT, pad: PAD });
  const xTicks = buildXTicks(min, max, presets.map((p) => p.size));
  const yTicks = buildYTicks(lo, hi);
  const presetSizes = new Set(presets.map((p) => p.size));

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      className="w-full"
      role="img"
      aria-label="Box price by size"
    >
      {/* Gridlines + y labels */}
      {yTicks.map((tick) => (
        <g key={`y-${tick}`}>
          <line
            x1={PAD.left}
            x2={WIDTH - PAD.right}
            y1={scales.y(tick)}
            y2={scales.y(tick)}
            stroke="var(--color-border-light)"
            strokeWidth={1}
          />
          <text
            x={PAD.left - 8}
            y={scales.y(tick) + 3.5}
            textAnchor="end"
            fontSize={10}
            fill="var(--color-text-tertiary)"
            fontFamily="var(--font-mono)"
          >
            {formatUnsignedAmount(tick, currency)}
          </text>
        </g>
      ))}

      {/* X labels */}
      {xTicks.map((tick) => (
        <text
          key={`x-${tick}`}
          x={scales.x(tick)}
          y={HEIGHT - PAD.bottom + 16}
          textAnchor="middle"
          fontSize={10}
          fill={presetSizes.has(tick) ? 'var(--color-text-secondary)' : 'var(--color-text-tertiary)'}
          fontWeight={presetSizes.has(tick) ? 600 : 400}
          fontFamily="var(--font-mono)"
        >
          {tick}
        </text>
      ))}

      {/* Dashed formula line (what the maths alone would charge) */}
      <path
        d={linePath(sizes, scales.x, (s) => scales.y(formula(s)))}
        fill="none"
        stroke="var(--color-text-tertiary)"
        strokeWidth={1.25}
        strokeDasharray="4 4"
      />

      {/* Solid effective line (presets win at their size) */}
      <path
        d={linePath(sizes, scales.x, (s) => scales.y(effective(s)))}
        fill="none"
        stroke="var(--color-brand-primary)"
        strokeWidth={2}
      />

      {/* Preset dots. Saving annotations render ONLY when authored (Spec 076) —
          the mathematical formula-vs-price gap is never presented as one. */}
      {presets.map((preset) => {
        const cx = scales.x(preset.size);
        const cy = scales.y(preset.price);
        return (
          <g key={`preset-${preset.size}`}>
            <circle
              cx={cx}
              cy={cy}
              r={4.5}
              fill="var(--color-surface)"
              stroke="var(--color-brand-primary)"
              strokeWidth={2}
            />
            {preset.saving != null && preset.saving > 0 && (
              <text
                x={cx}
                y={cy - 10}
                textAnchor="middle"
                fontSize={10}
                fontWeight={600}
                fill="var(--color-success)"
                fontFamily="var(--font-mono)"
              >
                −{formatUnsignedAmount(preset.saving, currency)}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}
