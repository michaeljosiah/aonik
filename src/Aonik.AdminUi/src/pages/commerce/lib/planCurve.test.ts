import { describe, expect, it } from 'vitest';

import type { BoxPlanDto, BoxPlanPresetDto } from '@/types/commerce';

import {
  curveModel,
  draftFromPlan,
  effectivePrice,
  emptyDraft,
  formulaPrice,
  isDirty,
  marginalJumps,
  validatePlan,
  type PlanDraft,
} from './planCurve';

const preset = (size: number, price: number, extra: Partial<BoxPlanPresetDto> = {}) =>
  ({
    size,
    price,
    badge: null,
    blurb: null,
    savingAmount: null,
    sortOrder: 0,
    ...extra,
  }) as BoxPlanPresetDto;

const plan: PlanDraft = {
  bundleProductId: 'b1',
  minSize: 6,
  maxSize: 30,
  baseSize: 6,
  basePrice: 30,
  perSpacePrice: 4,
  currency: 'GBP',
  presets: [preset(10, 60, { savingAmount: 4 }), preset(20, 110)],
};

describe('formulaPrice / effectivePrice', () => {
  it('prices an unmerchandised size from the formula', () => {
    expect(formulaPrice(plan, 6)).toBe(30);
    expect(formulaPrice(plan, 8)).toBe(38);
    expect(effectivePrice(plan, 8)).toBe(38);
  });

  it('lets a PRESET win at its own size, above or below the formula', () => {
    // The formula is neither a floor nor a cap — 10 spaces would compute 46 and is
    // merchandised at 60; the plan means what it says.
    expect(formulaPrice(plan, 10)).toBe(46);
    expect(effectivePrice(plan, 10)).toBe(60);
    expect(effectivePrice(plan, 20)).toBe(110);
  });

  it('leaves the size either side of a preset on the formula', () => {
    expect(effectivePrice(plan, 9)).toBe(42);
    expect(effectivePrice(plan, 11)).toBe(50);
  });
});

describe('curveModel', () => {
  it('spans the WHOLE configured range, not just where the data sits', () => {
    // A preset at 20 or the formula tail must always be in view.
    const model = curveModel(plan);
    expect(model.sizes[0]).toBe(6);
    expect(model.sizes.at(-1)).toBe(30);
    expect(model.xTicks).toContain(30);
    expect(model.xTicks).toContain(20);
  });

  it('includes every preset size as a tick even when thinning skips it', () => {
    // Thinning is every 4th from min, so 10 is on the grid but 20 is not — 6, 10, 14, 18, 22…
    expect(curveModel(plan).xTicks).toEqual(expect.arrayContaining([10, 20]));
  });

  it('plots the EFFECTIVE price at each size', () => {
    const model = curveModel(plan);
    expect(model.points.find((p) => p.size === 10)?.price).toBe(60);
    expect(model.points.find((p) => p.size === 11)?.price).toBe(50);
  });

  it('carries the authored saving onto the marker, and null where none was authored', () => {
    const markers = curveModel(plan).presetMarkers;
    expect(markers.find((m) => m.size === 10)?.saving).toBe(4);
    expect(markers.find((m) => m.size === 20)?.saving).toBeNull();
  });

  it('bounds the y-domain around everything either line reaches', () => {
    const model = curveModel(plan);
    // Highest value anywhere is the formula at 30 (= 126); lowest is 30 at the base.
    expect(model.bounds.hi).toBeGreaterThan(126);
    expect(model.bounds.lo).toBeLessThan(30);
  });

  it('survives a degenerate min === max plan', () => {
    const single = { ...plan, minSize: 6, maxSize: 6, presets: [] };
    const model = curveModel(single);
    expect(model.sizes).toEqual([6]);
    expect(model.points).toEqual([{ size: 6, price: 30 }]);
    expect(model.bounds.hi).toBeGreaterThan(model.bounds.lo);
  });
});

describe('marginalJumps', () => {
  it('runs between the sizes that can change the answer', () => {
    // base → first preset, preset → preset, last preset → max.
    expect(marginalJumps(plan).map((j) => [j.from, j.to])).toEqual([
      [6, 10],
      [10, 20],
      [20, 30],
    ]);
  });

  it('is effective-price SUBTRACTION, not per-space × spaces', () => {
    // The rule the page exists to make legible. 6→10 is four spaces; per-space maths would say
    // 16, and the merchandised preset makes it 30.
    const [first] = marginalJumps(plan);
    expect(first.delta).toBe(30);
    expect(first.delta).not.toBe(4 * plan.perSpacePrice);
  });

  it('bends around a discounted preset — the following jump gets LARGER', () => {
    // A preset that undercuts its formula is cheap to reach and expensive to leave.
    const discounted: PlanDraft = { ...plan, presets: [preset(10, 40)] };
    const jumps = marginalJumps(discounted);
    expect(jumps[0].delta).toBe(10); // 6 → 10 costs 40 − 30
    expect(jumps[1].delta).toBe(86); // 10 → 30 costs 126 − 40
  });

  it('reports ONE jump for a plan with no presets, rather than three restatements', () => {
    const flat: PlanDraft = { ...plan, presets: [] };
    expect(marginalJumps(flat)).toHaveLength(1);
    expect(marginalJumps(flat)[0].delta).toBe(96);
  });

  it('returns nothing when there is nowhere to grow', () => {
    expect(marginalJumps({ ...plan, minSize: 6, maxSize: 6, presets: [] })).toEqual([]);
  });

  it('spreads across a crowded plan instead of showing only the bottom', () => {
    const many: PlanDraft = {
      ...plan,
      presets: [8, 12, 16, 24].map((s) => preset(s, 40 + s)),
    };
    const jumps = marginalJumps(many);
    expect(jumps).toHaveLength(3);
    expect(jumps.at(-1)!.to).toBe(30);
  });
});

describe('validatePlan', () => {
  it('accepts a well-formed plan', () => {
    expect(validatePlan(plan)).toBeNull();
  });

  it('requires the base size to be sellable', () => {
    expect(validatePlan({ ...plan, baseSize: 40 })).toMatch(/base size must be between 6 and 30/);
  });

  it('refuses a formula that quotes zero or less at the smallest box', () => {
    // The floor is at minSize, not baseSize — a steep per-space price with a low base can go
    // negative below the anchor and reach order creation before anything notices.
    const steep = { ...plan, minSize: 2, baseSize: 10, basePrice: 20, perSpacePrice: 4 };
    expect(validatePlan(steep)).toMatch(/every size must price above zero/);
  });

  it('rejects a preset outside the range, naming the size', () => {
    expect(validatePlan({ ...plan, presets: [preset(40, 200)] })).toMatch(/40 spaces is outside/);
  });

  it('rejects duplicate presets for one size', () => {
    expect(validatePlan({ ...plan, presets: [preset(10, 60), preset(10, 62)] })).toMatch(
      /two presets for 10 spaces/,
    );
  });

  it('rejects a negative authored saving but allows none at all', () => {
    expect(validatePlan({ ...plan, presets: [preset(10, 60, { savingAmount: -1 })] })).toMatch(
      /saving on the 10-space preset cannot be negative/,
    );
    expect(validatePlan({ ...plan, presets: [preset(10, 60, { savingAmount: null })] })).toBeNull();
  });

  it('rejects overlong badge and blurb rather than letting the column truncate', () => {
    expect(validatePlan({ ...plan, presets: [preset(10, 60, { badge: 'x'.repeat(65) })] })).toMatch(
      /badge on the 10-space preset/,
    );
    expect(validatePlan({ ...plan, presets: [preset(10, 60, { blurb: 'x'.repeat(257) })] })).toMatch(
      /blurb on the 10-space preset/,
    );
  });

  it('requires a 3-letter currency code', () => {
    expect(validatePlan({ ...plan, currency: 'POUNDS' })).toMatch(/3-letter ISO code/);
  });

  it('refuses an empty draft, so the seeded form cannot be saved as-is', () => {
    expect(validatePlan(emptyDraft('b1', 'GBP'))).not.toBeNull();
  });
});

describe('emptyDraft', () => {
  it('invents nothing but the tenant currency', () => {
    const draft = emptyDraft('b1', 'NGN');
    expect(draft.currency).toBe('NGN');
    expect(draft.presets).toEqual([]);
    expect([draft.minSize, draft.maxSize, draft.basePrice, draft.perSpacePrice]).toEqual([0, 0, 0, 0]);
  });
});

describe('isDirty', () => {
  const saved = {
    bundleProductId: 'b1',
    minSize: 6,
    maxSize: 30,
    baseSize: 6,
    basePrice: 30,
    perSpacePrice: 4,
    currency: 'GBP',
    presets: [preset(20, 110), preset(10, 60, { savingAmount: 4 })],
  } as BoxPlanDto;

  it('is false for an untouched draft, whatever order the server sent presets in', () => {
    expect(isDirty(draftFromPlan(saved), saved)).toBe(false);
  });

  it('notices a changed figure and a changed preset', () => {
    expect(isDirty({ ...draftFromPlan(saved), perSpacePrice: 5 }, saved)).toBe(true);
    expect(
      isDirty({ ...draftFromPlan(saved), presets: [preset(10, 61), preset(20, 110)] }, saved),
    ).toBe(true);
  });

  it('treats a plan that does not exist yet as dirty — there is nothing to match', () => {
    expect(isDirty(emptyDraft('b1', 'GBP'), null)).toBe(true);
  });
});
