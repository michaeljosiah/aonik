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
  type FigureKey,
  type HeatingStep,
} from './contentState';

export interface ContentDraft {
  servingLabel: string;
  figures: Record<FigureKey, string>;
  ingredients: string;
  allergens: string;
  heating: HeatingStep[];
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
    heatingJson: heatingToWire(draft.heating),
  };
}
