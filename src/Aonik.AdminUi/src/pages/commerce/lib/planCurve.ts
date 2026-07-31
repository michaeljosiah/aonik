// Box-plan pricing maths (Spec 076 §3) — the plan domain only.
//
// Scale and tick derivation already live in `components/priceCurveMath.ts`, which Spec 073's
// shared <PriceCurve/> renders from. This module deliberately does NOT restate them: two
// sources of truth for one chart is how the axis and the dots stop agreeing. What lives here is
// what the plan itself means — which price is charged at a size, and what growing a box costs.
//
// The two rules the whole page exists to make legible:
//
//   A PRESET WINS AT ITS SIZE. Every other size in [min, max] prices as
//   basePrice + (size − baseSize) × perSpacePrice. The formula is not a floor or a cap; it is
//   simply what applies where nothing was merchandised.
//
//   GROWING A BOX CHARGES effective(target) − effective(current), never perSpace × spaces.
//   Those differ wherever a preset sits between the two sizes, which is exactly where an
//   operator's intuition fails — hence the marginal strip.

import type { BoxPlanDto, BoxPlanPresetDto } from '@/types/commerce';
import {
  buildXTicks,
  buildYTicks,
  curveSizes,
  priceBounds,
  type PriceCurvePreset,
} from '../components/priceCurveMath';

/** The editable shape the page holds: a plan whose numbers are still being typed. */
export interface PlanDraft {
  bundleProductId: string;
  minSize: number;
  maxSize: number;
  baseSize: number;
  basePrice: number;
  perSpacePrice: number;
  currency: string;
  presets: BoxPlanPresetDto[];
}

export function draftFromPlan(plan: BoxPlanDto): PlanDraft {
  return {
    bundleProductId: plan.bundleProductId,
    minSize: plan.minSize,
    maxSize: plan.maxSize,
    baseSize: plan.baseSize,
    basePrice: plan.basePrice,
    perSpacePrice: plan.perSpacePrice,
    currency: plan.currency,
    presets: [...plan.presets].sort((a, b) => a.size - b.size),
  };
}

/**
 * A blank plan for a bundle that has none.
 *
 * Nothing is invented: sizes and prices are zero so every field reads as unfilled and the
 * client validation refuses the save until a person supplies them. Only the currency is
 * defaulted, from the storefront's canonical code, because that is a tenant fact rather than a
 * guess about this bundle.
 */
export function emptyDraft(bundleProductId: string, currency: string): PlanDraft {
  return {
    bundleProductId,
    minSize: 0,
    maxSize: 0,
    baseSize: 0,
    basePrice: 0,
    perSpacePrice: 0,
    currency,
    presets: [],
  };
}

/** basePrice + (size − baseSize) × perSpacePrice — what the maths alone would charge. */
export function formulaPrice(plan: PlanDraft, size: number): number {
  return plan.basePrice + (size - plan.baseSize) * plan.perSpacePrice;
}

/** The price actually charged: a preset at this exact size, otherwise the formula. */
export function effectivePrice(plan: PlanDraft, size: number): number {
  const preset = plan.presets.find((p) => p.size === size);
  return preset ? preset.price : formulaPrice(plan, size);
}

export interface CurveModel {
  sizes: number[];
  xTicks: number[];
  yTicks: number[];
  /** One point per size on the EFFECTIVE line. */
  points: { size: number; price: number }[];
  presetMarkers: PriceCurvePreset[];
  bounds: { lo: number; hi: number };
}

/**
 * Everything the curve needs, derived from the draft rather than the saved plan — so the
 * operator sees the shape of a change before committing to it.
 */
export function curveModel(plan: PlanDraft): CurveModel {
  const sizes = curveSizes(plan.minSize, plan.maxSize);
  const formula = (size: number) => formulaPrice(plan, size);
  const effective = (size: number) => effectivePrice(plan, size);
  const presetMarkers: PriceCurvePreset[] = plan.presets.map((p) => ({
    size: p.size,
    price: p.price,
    saving: p.savingAmount,
  }));
  const bounds = priceBounds(sizes, formula, effective, presetMarkers);

  return {
    sizes,
    // The plotted domain is the WHOLE configured range, so a preset at the far end and the
    // formula tail are always in view rather than cropped to where the data happens to be.
    xTicks: buildXTicks(plan.minSize, plan.maxSize, presetMarkers.map((p) => p.size)),
    yTicks: buildYTicks(bounds.lo, bounds.hi),
    points: sizes.map((size) => ({ size, price: effective(size) })),
    presetMarkers,
    bounds,
  };
}

export interface MarginalJump {
  from: number;
  to: number;
  fromPrice: number;
  toPrice: number;
  /** effective(to) − effective(from) — what growing from one to the other costs. */
  delta: number;
  /** What makes this jump representative, in the operator's terms. */
  note: string;
}

/**
 * Up to three jumps that show where the marginal cost bends.
 *
 * The anchors are the sizes that can change the answer — the formula base, every preset, and
 * the top of the range — and the jumps run between consecutive anchors. That yields the spec's
 * three cases without hard-coding them: base to the first preset, preset to preset, and the
 * last preset onward to the maximum. A plan with no presets produces the single base-to-max
 * jump, which is the honest answer rather than three restatements of perSpacePrice.
 */
export function marginalJumps(plan: PlanDraft): MarginalJump[] {
  if (plan.maxSize <= plan.minSize) return [];

  const anchors = Array.from(
    new Set(
      [plan.baseSize, ...plan.presets.map((p) => p.size), plan.maxSize].filter(
        (size) => size >= plan.minSize && size <= plan.maxSize,
      ),
    ),
  ).sort((a, b) => a - b);

  const pairs: [number, number][] = [];
  for (let i = 0; i < anchors.length - 1; i += 1) pairs.push([anchors[i], anchors[i + 1]]);
  if (pairs.length === 0) return [];

  // First, middle and last — spread rather than the first three, so a plan with many presets
  // still shows the top of its range.
  const chosen =
    pairs.length <= 3
      ? pairs
      : [pairs[0], pairs[Math.floor(pairs.length / 2)], pairs[pairs.length - 1]];

  const presetSizes = new Set(plan.presets.map((p) => p.size));
  return chosen.map(([from, to]) => ({
    from,
    to,
    fromPrice: effectivePrice(plan, from),
    toPrice: effectivePrice(plan, to),
    delta: effectivePrice(plan, to) - effectivePrice(plan, from),
    note: presetSizes.has(to)
      ? `into the ${to}-space preset`
      : presetSizes.has(from)
        ? `out of the ${from}-space preset`
        : 'on the formula',
  }));
}

/**
 * What the server will refuse, checked here so a rejected plan is not presented as saved.
 *
 * The codes are the service's own (A1/A2/A5) and the messages say what an operator can act on.
 * This is a courtesy: `BundleSizePlanService.ValidateStructure` revalidates everything, and A4
 * — the open-box-session currency guard — is deliberately NOT mirrored, because only the server
 * can count live sessions and a client guess would either block a legal save or promise an
 * illegal one.
 */
export function validatePlan(plan: PlanDraft): string | null {
  if (plan.minSize < 1) return 'The smallest box must hold at least one space.';
  if (plan.maxSize < plan.minSize) {
    return 'The largest box cannot be smaller than the smallest.';
  }
  if (plan.baseSize < plan.minSize || plan.baseSize > plan.maxSize) {
    return `The base size must be between ${plan.minSize} and ${plan.maxSize} — it is the size the base price is quoted for.`;
  }
  if (plan.basePrice <= 0) return 'The base price must be greater than zero.';
  if (plan.perSpacePrice < 0) return 'The per-space price cannot be negative.';

  // The formula's lowest point across the range. A plan whose smallest box quotes zero or less
  // would reach order and payment creation before anything noticed.
  const floor = formulaPrice(plan, plan.minSize);
  if (floor <= 0) {
    return `At ${plan.minSize} spaces the formula quotes ${floor.toFixed(2)} — every size must price above zero.`;
  }

  if (!/^[A-Za-z]{3}$/.test(plan.currency.trim())) {
    return 'The currency must be a 3-letter ISO code, such as GBP.';
  }

  const seen = new Set<number>();
  for (const preset of plan.presets) {
    if (preset.size < plan.minSize || preset.size > plan.maxSize) {
      return `The preset at ${preset.size} spaces is outside the sellable range, so it could never be quoted.`;
    }
    if (seen.has(preset.size)) return `There are two presets for ${preset.size} spaces.`;
    seen.add(preset.size);
    if (preset.price <= 0) {
      return `The preset at ${preset.size} spaces must price above zero.`;
    }
    if (preset.savingAmount != null && preset.savingAmount < 0) {
      return `The saving on the ${preset.size}-space preset cannot be negative.`;
    }
    if ((preset.badge?.length ?? 0) > BADGE_MAX) {
      return `The badge on the ${preset.size}-space preset is longer than ${BADGE_MAX} characters.`;
    }
    if ((preset.blurb?.length ?? 0) > BLURB_MAX) {
      return `The blurb on the ${preset.size}-space preset is longer than ${BLURB_MAX} characters.`;
    }
  }
  return null;
}

/** nvarchar(64) / nvarchar(256) — the service rejects overlong text rather than truncating. */
export const BADGE_MAX = 64;
export const BLURB_MAX = 256;

/** True when the draft differs from what the server holds. */
export function isDirty(draft: PlanDraft, saved: BoxPlanDto | null): boolean {
  if (!saved) return true;
  return JSON.stringify(normalise(draft)) !== JSON.stringify(normalise(draftFromPlan(saved)));
}

function normalise(plan: PlanDraft) {
  return {
    ...plan,
    presets: [...plan.presets]
      .sort((a, b) => a.size - b.size)
      .map((p) => ({
        size: p.size,
        price: p.price,
        badge: p.badge ?? null,
        blurb: p.blurb ?? null,
        savingAmount: p.savingAmount ?? null,
      })),
  };
}
