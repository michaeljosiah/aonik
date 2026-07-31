// Option pricing derivation (Spec 074 §3). ONE function, used by both the choices table and
// the narrowing Sheet, because two implementations of "what does this cost extra" eventually
// disagree and the operator has no way to tell which screen is lying.
//
// Spec 066 §8: stored prices are ABSOLUTE per-unit amounts. Every "+£1.50" on screen is
// derived — never stored, never authored — and the baseline it derives against is the
// EFFECTIVE default, not the group's. A product that pins its own defaultChoiceKey moves the
// zero point for that product, so the same choice legitimately reads +£1.50 in the catalogue
// table and −£0.50 inside a product's Sheet.

export interface PricedChoice {
  key: string;
  price: number;
  isRecommendedDefault: boolean;
  isActive: boolean;
}

/**
 * The signed difference from the baseline. Null when there is no baseline to derive against —
 * the caller renders nothing rather than an unqualified absolute price, which would read as a
 * delta and overstate every choice by the default's price.
 */
export function choiceDelta(
  choice: Pick<PricedChoice, 'price'>,
  effectiveDefault: Pick<PricedChoice, 'price'> | null | undefined,
): number | null {
  if (!effectiveDefault) return null;
  return choice.price - effectiveDefault.price;
}

/**
 * Which choice is the baseline for a product, given its stored narrowing line.
 *
 * `defaultChoiceKey` on the line is a product-level override of the group's recommended
 * default. It is resolved against the group's choices, and a key that no longer matches any
 * choice falls back to the group default rather than leaving the product without a baseline —
 * an option retired since the narrowing was authored must not blank every delta on the page.
 */
export function effectiveDefaultChoice<T extends PricedChoice>(
  choices: readonly T[],
  overrideKey?: string | null,
): T | null {
  if (overrideKey) {
    const pinned = choices.find((choice) => choice.key === overrideKey);
    if (pinned) return pinned;
  }
  return choices.find((choice) => choice.isRecommendedDefault && choice.isActive) ?? null;
}

/**
 * The choices a product actually offers for a group.
 *
 * `allowedChoiceKeys === null` is NOT "none" — it means inherit every active choice, including
 * ones added to the catalogue later. Collapsing that to an explicit list is the exact data
 * loss the raw-narrowing read exists to prevent, so the distinction is preserved here rather
 * than resolved away.
 */
export function offeredChoices<T extends PricedChoice>(
  choices: readonly T[],
  allowedChoiceKeys: readonly string[] | null,
): T[] {
  const active = choices.filter((choice) => choice.isActive);
  if (allowedChoiceKeys === null) return active;
  const allowed = new Set(allowedChoiceKeys);
  return active.filter((choice) => allowed.has(choice.key));
}

/** True when the group offers the operator nothing to pick — its rail card warns. */
export function hasNoActiveChoices(choices: readonly PricedChoice[]): boolean {
  return !choices.some((choice) => choice.isActive);
}
