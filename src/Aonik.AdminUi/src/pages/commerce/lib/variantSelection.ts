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

/** How many groups the operator has actually picked something for. */
export function pickedGroupCount(selection: Record<string, SelectionValue>): number {
  return Object.values(selection).filter((value) => !isEmptySelection(value)).length;
}
