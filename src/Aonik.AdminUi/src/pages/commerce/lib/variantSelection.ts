// Variant selection shaping (Spec 075 §2).
//
// A group's selection value is NOT always a string. `OptionSelectionService.ResolveAsync`
// rejects a bare string for a `Multi` group (V5) and rejects an array for a `One` group, so the
// wire shape is decided by the group's selectionMode — not by how many choices happen to be
// picked. A one-element multi-select is still an array.
//
// This module exists because the first implementation read an array and kept only `value[0]`,
// which made every multi-select combination unauthorable and every existing multi-select
// variant uneditable: the form could not represent them, and what it did send was refused.
//
// Canonicalisation stays SERVER-side. This only shapes what the operator picked; the service
// normalises through Spec 066 and stores the complete canonical selection.

export const MULTI_MODE = 'Multi';

/** One group's picked value, as the form holds it. */
export type SelectionValue = string | string[];

export interface SelectionGroup {
  key: string;
  selectionMode: string;
}

export function isMulti(group: SelectionGroup): boolean {
  return group.selectionMode === MULTI_MODE;
}

/**
 * Stored/canonical selection JSON to the form's map.
 *
 * Values are kept in the SHAPE they arrived in — an array stays an array — because that shape
 * is what the group's mode requires on the way back out.
 */
export function parseSelection(json: string | null | undefined): Record<string, SelectionValue> {
  if (!json) return {};
  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch {
    return {};
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};

  const out: Record<string, SelectionValue> = {};
  for (const [key, value] of Object.entries(parsed as Record<string, unknown>)) {
    if (typeof value === 'string') out[key] = value;
    else if (Array.isArray(value)) out[key] = value.filter((v): v is string => typeof v === 'string');
  }
  return out;
}

/** True when this group has nothing picked — the two shapes are empty differently. */
export function isEmptySelection(value: SelectionValue | undefined): boolean {
  if (value === undefined) return true;
  return Array.isArray(value) ? value.length === 0 : value === '';
}

/**
 * The form's map to wire JSON, shaped per group mode.
 *
 * A `Multi` group is always an ARRAY, even with one choice picked — the mode decides the shape,
 * not the cardinality, and a bare string there is a V5 rejection.
 */
export function serialiseSelection(
  selection: Record<string, SelectionValue>,
  groups: readonly SelectionGroup[],
): string {
  const payload: Record<string, string | string[]> = {};
  for (const group of groups) {
    const value = selection[group.key];
    if (isEmptySelection(value)) continue;
    payload[group.key] = isMulti(group)
      ? Array.isArray(value)
        ? value
        : [value as string]
      : Array.isArray(value)
        ? (value[0] as string)
        : (value as string);
  }
  return JSON.stringify(payload);
}

/** Toggling one choice within a multi-select group. */
export function toggleMulti(current: SelectionValue | undefined, choiceKey: string): string[] {
  const list = Array.isArray(current) ? current : current ? [current] : [];
  return list.includes(choiceKey) ? list.filter((k) => k !== choiceKey) : [...list, choiceKey];
}

/**
 * Ways a STORED selection can no longer be expressed against the product's current offer.
 *
 * All three are silent otherwise: `serialiseSelection` iterates the current groups and coerces
 * shapes, so a removed group is dropped, a withdrawn choice is passed through to be rejected,
 * and a Multi→One mode change TRUNCATES a multi-choice selection to its first member. The last
 * is the worst — every group and choice remains valid, so nothing looks wrong, and the update
 * API accepts selection changes: the variant moves onto the single-choice combination carrying
 * figures and allergens authored for the multi-choice one.
 */
export interface SelectionDrift {
  /** Groups the product no longer offers at all. */
  missingGroups: string[];
  /** `group.choice` pairs whose choice is no longer offered. */
  withdrawnChoices: string[];
  /** Groups whose selection mode can no longer represent what is stored. */
  shapeChanged: string[];
  /**
   * Groups the product has GAINED since the selection was stored.
   *
   * The only drift not visible in the stored selection itself — it is an absence, so a loop over
   * what is stored can never see it. A stored canonical selection names every effective group,
   * so a group present in the offer and missing from it appeared afterwards. Re-serialising the
   * old partial selection lets server normalisation supply the new group's default, and the
   * variant retargets onto a preparation that did not exist when its allergens were written.
   */
  addedGroups: string[];
}

export interface DriftGroup extends SelectionGroup {
  choices: readonly { key: string }[];
}

export interface DriftOptions {
  /**
   * The selection is a COMPLETE canonical one read back from the server, so an effective group
   * it does not name is a group that was added later.
   *
   * False while the operator composes a new combination: a partial selection is the intended
   * input there — the server fills the rest — and treating absence as drift would refuse every
   * create.
   */
  storedIsCanonical?: boolean;
}

export function detectSelectionDrift(
  selection: Record<string, SelectionValue>,
  groups: readonly DriftGroup[],
  options: DriftOptions = {},
): SelectionDrift {
  const missingGroups: string[] = [];
  const withdrawnChoices: string[] = [];
  const shapeChanged: string[] = [];
  const addedGroups: string[] = [];

  for (const [key, value] of Object.entries(selection)) {
    if (isEmptySelection(value)) continue;
    const group = groups.find((g) => g.key === key);
    if (!group) {
      missingGroups.push(key);
      continue;
    }
    const picked = Array.isArray(value) ? value : [value];
    for (const choiceKey of picked) {
      if (!group.choices.some((c) => c.key === choiceKey)) {
        withdrawnChoices.push(`${key}.${choiceKey}`);
      }
    }
    // More than one choice stored for a group that is now single-select cannot survive
    // serialisation — and would survive it QUIETLY, as a different combination.
    if (!isMulti(group) && picked.length > 1) shapeChanged.push(key);
  }

  if (options.storedIsCanonical) {
    for (const group of groups) {
      // A group with no offerable choices cannot be defaulted into anything, so its absence
      // says nothing about when it appeared.
      if (group.choices.length === 0) continue;
      if (isEmptySelection(selection[group.key])) addedGroups.push(group.key);
    }
  }

  return { missingGroups, withdrawnChoices, shapeChanged, addedGroups };
}

export function hasDrift(drift: SelectionDrift): boolean {
  return (
    drift.missingGroups.length > 0 ||
    drift.withdrawnChoices.length > 0 ||
    drift.shapeChanged.length > 0 ||
    drift.addedGroups.length > 0
  );
}

export function describeDrift(drift: SelectionDrift): string {
  const parts: string[] = [];
  if (drift.missingGroups.length > 0) {
    parts.push(`${drift.missingGroups.join(', ')} (no longer offered)`);
  }
  if (drift.withdrawnChoices.length > 0) {
    parts.push(`${drift.withdrawnChoices.join(', ')} (choice withdrawn)`);
  }
  if (drift.shapeChanged.length > 0) {
    parts.push(`${drift.shapeChanged.join(', ')} (now single-select, but several are stored)`);
  }
  if (drift.addedGroups.length > 0) {
    parts.push(`${drift.addedGroups.join(', ')} (offered since this combination was authored)`);
  }
  return parts.join('; ');
}

/** How many groups the operator has actually picked something for. */
export function pickedGroupCount(selection: Record<string, SelectionValue>): number {
  return Object.values(selection).filter((value) => !isEmptySelection(value)).length;
}
