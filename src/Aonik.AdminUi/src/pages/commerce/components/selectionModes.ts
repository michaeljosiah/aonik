// The selection-mode wire values, in one place because the labels and the values are NOT the
// same words and getting that wrong is invisible until a save fails.
//
// `OptionSelectionModes` accepts "One" and "Multi" (OptionGroup.cs:48,51) and rejects anything
// else with V12. The obvious English words — "Single" and "Multiple" — are exactly what a
// reader assumes and exactly what the server refuses, so the display label is kept separate
// from the value rather than derived from it.

export interface SelectionModeOption {
  /** The wire value. Must match OptionSelectionModes. */
  value: string;
  /** What the operator reads. */
  label: string;
}

export const SELECTION_MODES: SelectionModeOption[] = [
  { value: 'One', label: 'Single — one choice' },
  { value: 'Multi', label: 'Multiple — any number' },
];
