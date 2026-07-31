import { describe, expect, it } from 'vitest';

import {
  choiceDelta,
  effectiveDefaultChoice,
  hasNoActiveChoices,
  offeredChoices,
  type PricedChoice,
} from './optionPricing';

function choice(overrides: Partial<PricedChoice> & { key: string }): PricedChoice {
  return { price: 0, isRecommendedDefault: false, isActive: true, ...overrides };
}

const RICE = choice({ key: 'rice', price: 8.5, isRecommendedDefault: true });
const YAM = choice({ key: 'yam', price: 10 });
const PLANTAIN = choice({ key: 'plantain', price: 7.25 });

describe('choiceDelta', () => {
  it('derives the signed difference from a NONZERO default price', () => {
    // The acceptance case: an absolute 10.00 against an absolute 8.50 default is +1.50,
    // never 10.00. Rendering the stored price as the delta overstates every choice.
    expect(choiceDelta(YAM, RICE)).toBe(1.5);
    expect(choiceDelta(PLANTAIN, RICE)).toBe(-1.25);
    expect(choiceDelta(RICE, RICE)).toBe(0);
  });

  it('returns null when there is no baseline rather than leaking an absolute price', () => {
    // An unqualified absolute reads as a delta on a screen whose column is headed "vs default".
    expect(choiceDelta(YAM, null)).toBeNull();
    expect(choiceDelta(YAM, undefined)).toBeNull();
  });
});

describe('effectiveDefaultChoice', () => {
  const choices = [RICE, YAM, PLANTAIN];

  it("uses the group's recommended default when the product pins nothing", () => {
    expect(effectiveDefaultChoice(choices)?.key).toBe('rice');
    expect(effectiveDefaultChoice(choices, null)?.key).toBe('rice');
  });

  it("a product's own default MOVES the zero point", () => {
    // Same choice, two truthful readings: +1.50 in the catalogue, 0 inside this product.
    const pinned = effectiveDefaultChoice(choices, 'yam');
    expect(pinned?.key).toBe('yam');
    expect(choiceDelta(YAM, pinned)).toBe(0);
    expect(choiceDelta(RICE, pinned)).toBe(-1.5);
  });

  it('falls back to the group default when the pinned key no longer exists', () => {
    // An option retired since the narrowing was authored must not blank every delta.
    expect(effectiveDefaultChoice(choices, 'gone')?.key).toBe('rice');
  });

  it('ignores a RETIRED recommended default rather than baselining against it', () => {
    const retiredDefault = [
      choice({ key: 'rice', price: 8.5, isRecommendedDefault: true, isActive: false }),
      YAM,
    ];
    expect(effectiveDefaultChoice(retiredDefault)).toBeNull();
  });

  it('returns null when a group has no default at all', () => {
    expect(effectiveDefaultChoice([YAM, PLANTAIN])).toBeNull();
  });
});

describe('offeredChoices', () => {
  const choices = [RICE, YAM, choice({ key: 'retired', price: 5, isActive: false })];

  it('treats NULL as inherit-every-active-choice, not as none', () => {
    // Collapsing null to an explicit list is the data loss the raw narrowing read prevents:
    // an inherited product picks up future catalogue choices, a pinned one never does.
    expect(offeredChoices(choices, null).map((c) => c.key)).toEqual(['rice', 'yam']);
  });

  it('treats an EMPTY list as offering nothing', () => {
    expect(offeredChoices(choices, [])).toEqual([]);
  });

  it('honours an explicit list and never revives a retired choice through it', () => {
    expect(offeredChoices(choices, ['yam', 'retired']).map((c) => c.key)).toEqual(['yam']);
  });
});

describe('hasNoActiveChoices', () => {
  it('detects a group whose choices are all retired', () => {
    expect(hasNoActiveChoices([choice({ key: 'a', isActive: false })])).toBe(true);
    expect(hasNoActiveChoices([RICE])).toBe(false);
    expect(hasNoActiveChoices([])).toBe(true);
  });
});
