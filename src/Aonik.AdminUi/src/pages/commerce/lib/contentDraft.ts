// The editable shape both content sheets bind to, and the mapping between it and the wire.
//
// Extracted from the components so the rules live somewhere testable — and because the block
// upsert is a FULL REPLACE across eleven members, which makes "what exactly does this form
// send" the single most consequential question on the page.

import type { ProductContentDto, ProductContentVariantDto } from '@/types/commerce';

import { NUTRITION_FIGURE_RULE, validateDecimalInput } from './decimalInput';
import {
  FIGURE_FIELDS,
  figureToInput,
  figureToWire,
  heatingToWire,
  incompleteHeatingRows,
  type FigureKey,
  type HeatingStep,
} from './contentState';

export interface ContentDraft {
  servingLabel: string;
  figures: Record<FigureKey, string>;
  ingredients: string;
  allergens: string;
  heating: HeatingStep[];
  /**
   * The source stored an AUTHORED EMPTY heating panel (`[]`), not a withheld one (`null`).
   *
   * A variant preserves that difference and the resolver reports it: `[]` resolves with
   * heatingWithheld false, `null` with true. Collapsing both into "no rows" meant editing an
   * unrelated figure silently converted an authored empty panel into a withheld one — a
   * customer-facing change nobody asked for, from a form that shows no heating either way.
   */
  heatingAuthoredEmpty: boolean;
}

export function emptyDraft(): ContentDraft {
  return {
    servingLabel: '',
    figures: {
      kcal: '',
      proteinGrams: '',
      carbsGrams: '',
      fatGrams: '',
      fibreGrams: '',
      sugarsGrams: '',
      saltGrams: '',
    },
    ingredients: '',
    allergens: '',
    heating: [],
    heatingAuthoredEmpty: false,
  };
}

function draftFrom(
  source: Pick<ProductContentDto, 'servingLabel' | 'nutrition' | 'ingredients' | 'allergens'> & {
    heating: ProductContentDto['heating'] | null;
  },
): ContentDraft {
  return {
    servingLabel: source.servingLabel,
    figures: Object.fromEntries(
      FIGURE_FIELDS.map((f) => [f.key, figureToInput(source.nutrition[f.key as FigureKey])]),
    ) as ContentDraft['figures'],
    ingredients: source.ingredients ?? '',
    allergens: source.allergens ?? '',
    heating: (source.heating ?? []).map((step) => ({ method: step.method, body: step.body })),
    heatingAuthoredEmpty: Array.isArray(source.heating) && source.heating.length === 0,
  };
}

export function draftFromBlock(block: ProductContentDto | null): ContentDraft {
  return block ? draftFrom(block) : emptyDraft();
}

export function draftFromVariant(variant: ProductContentVariantDto): ContentDraft {
  return draftFrom(variant);
}

/**
 * Everything that must hold before any write. Figures are `decimal(9,2)` with the service's
 * own bound, and an over-precise value is ROUNDED by the database rather than refused — so a
 * save would report success for a panel the store does not hold.
 */
export function validateDraft(draft: ContentDraft): string | null {
  if (!draft.servingLabel.trim()) return 'A serving label is required — it captions every figure.';
  for (const field of FIGURE_FIELDS) {
    const message = validateDecimalInput(draft.figures[field.key as FigureKey], {
      ...NUTRITION_FIGURE_RULE,
      subject: field.label,
    });
    if (message) return message;
  }
  // ParseHeatingStrict requires both halves, and rejects the WHOLE save over one bad row.
  const partial = incompleteHeatingRows(draft.heating);
  if (partial.length > 0) {
    return `Heating step ${partial.join(', ')} needs both a method and an instruction — or clear both to drop it.`;
  }
  return null;
}

/**
 * The wire members every content write shares.
 *
 * Blank text becomes NULL rather than an empty string, because null is "withheld" while `""`
 * would be an authored empty declaration — and blank figures become null rather than 0, which
 * would publish a nutrition claim nobody made.
 */
export function wireFromDraft(draft: ContentDraft) {
  return {
    servingLabel: draft.servingLabel.trim(),
    ...(Object.fromEntries(
      FIGURE_FIELDS.map((f) => [f.key, figureToWire(draft.figures[f.key as FigureKey])]),
    ) as Record<FigureKey, number | null>),
    ingredients: draft.ingredients.trim() === '' ? null : draft.ingredients.trim(),
    allergens: draft.allergens.trim() === '' ? null : draft.allergens.trim(),
    // An authored-empty panel is resent as `[]` rather than null, so an edit elsewhere on the
    // form does not turn "no heating required" into "heating withheld".
    heatingJson: heatingToWire(draft.heating) ?? (draft.heatingAuthoredEmpty ? '[]' : null),
  };
}
