// What a product's option offer means to content authoring (Spec 075).
//
// Content is bound to a PREPARATION, and which preparation a save lands on is decided by the
// offer at write time — not by the offer the sheet loaded. Both authoring sheets hold a
// page-load snapshot of the effective groups for as long as they are open, and an option write
// by another operator moves the target underneath them without touching anything either sheet
// versions:
//
//   - the default block describes the effective DEFAULT combination, which has no row and no
//     version of its own; when no block exists yet there is not even a ContentVersion or review
//     flag for the option write to have disturbed;
//   - a variant's stored selection is completed by server normalisation, so a group added after
//     the sheet opened is filled with its default and the variant retargets.
//
// This module turns the part of the offer that decides that into a comparable value, so a sheet
// can capture it on open and refuse when it no longer matches.

export interface OfferGroup {
  key: string;
  defaultChoiceKey: string;
  choices: readonly { key: string }[];
}

/**
 * The combination a product serves when the customer picks nothing — the preparation the
 * default block describes.
 *
 * Groups with no offerable choices contribute nothing: they cannot be defaulted into a
 * selection, so they cannot change which preparation is served.
 */
export function defaultCombination(groups: readonly OfferGroup[]): Record<string, string> {
  const out: Record<string, string> = {};
  for (const group of groups) {
    if (group.choices.length === 0 || !group.defaultChoiceKey) continue;
    out[group.key] = group.defaultChoiceKey;
  }
  return out;
}

/** Key-order-independent, so a reordered offer is not mistaken for a changed one. */
export function defaultSignature(groups: readonly OfferGroup[]): string {
  const combination = defaultCombination(groups);
  return Object.keys(combination)
    .sort()
    .map((key) => `${key}=${combination[key]}`)
    .join('|');
}
