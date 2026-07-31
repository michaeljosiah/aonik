// Rendering a canonical selection as something a person can check (Spec 075).
//
// The block's figures and declarations are published against ONE preparation, and every
// precondition on this page is expressed as that preparation's canonical selection JSON. An
// operator confirming "reviewed, still correct" is asserting something about it — so it has to
// be on screen, not merely enforced. V-C9 proves the binding did not move during the request;
// it says nothing about whether a human looked at what they were agreeing to.

import { parseSelection, type SelectionValue } from './variantSelection';

export interface PreparationLabelSource {
  key: string;
  label: string;
  choices: readonly { key: string; label: string }[];
}

export interface PreparationLine {
  group: string;
  choice: string;
}

/**
 * The choices a canonical selection names, labelled where the offer is available.
 *
 * Falls back to raw keys rather than hiding a group: an unlabelled `protein = salmon` is still
 * checkable, and dropping it would understate what is being confirmed.
 */
export function describePreparation(
  selectionJson: string,
  groups: readonly PreparationLabelSource[] = [],
): PreparationLine[] {
  const selection = parseSelection(selectionJson);
  return Object.entries(selection).map(([groupKey, value]) => {
    const group = groups.find((g) => g.key === groupKey);
    return {
      group: group?.label ?? groupKey,
      choice: labelChoices(value, group),
    };
  });
}

function labelChoices(value: SelectionValue, group?: PreparationLabelSource): string {
  const keys = Array.isArray(value) ? value : [value];
  return keys
    .map((key) => group?.choices.find((c) => c.key === key)?.label ?? key)
    .join(', ');
}
