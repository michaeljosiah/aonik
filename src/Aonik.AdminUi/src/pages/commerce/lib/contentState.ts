// Content state derivation (Spec 075 §3). Pure and tested, because this mapper decides whether
// the admin tells an operator their allergen text is live — and getting that wrong is a safety
// claim, not a cosmetic one.
//
// Spec 067's model, which the precedence below encodes exactly:
//   * FIGURES may fall back, captioned as the standard preparation.
//   * DECLARATIONS (ingredients, allergens, heating) are exact-authored or WITHHELD. Never
//     substituted, never inherited, never derived.
//
// The staleness input is SERVER-COMPUTED and must stay that way. It evaluates
// `requiresReview OR describesSelectionJson !== the product's current all-defaults canonical
// selection`, and the second half needs Spec 066 canonicalisation. A client re-implementation
// would drift from the resolver, and the direction it would drift is the dangerous one:
// labelling a withholding block "Authored".

import type { ProductContentDto } from '@/types/commerce';

export type ContentState = 'none' | 'authored' | 'review' | 'withheld';

/**
 * @param block The RAW stored block, or null when the admin read returned no block at all.
 * @param isStale The server's verdict. True means the resolver is withholding declarations.
 */
export function deriveContentState(
  block: Pick<ProductContentDto, 'ingredients' | 'allergens'> | null | undefined,
  isStale: boolean,
): ContentState {
  // NO BLOCK — not "no figures". A block whose figures are all null is still a block, and the
  // resolver may be serving its declarations right now; collapsing the two would offer an
  // "Author the default block" CTA that overwrites live customer-visible safety content.
  if (!block) return 'none';

  // Covers BOTH the explicit requiresReview flag and a binding that no longer matches the
  // current defaults. The resolver withholds in either case, so neither may read as Authored.
  if (isStale) return 'review';

  // Withheld means the declarations are absent, whatever the figures do.
  if (block.ingredients === null && block.allergens === null) return 'withheld';

  return 'authored';
}

/** What the workbench renders for a declaration cell. */
export type DeclarationRender =
  | { kind: 'authored'; text: string }
  | { kind: 'withheld-review' }
  | { kind: 'absent' };

/**
 * A declaration's rendering, which is NOT simply "text or dash".
 *
 * Under review the text is withheld from customers, so showing it here as though it were live
 * would tell the operator their allergen line is serving when it is not. Absent and withheld
 * are likewise different states with different fixes, so they read differently.
 */
export function renderDeclaration(
  text: string | null | undefined,
  state: ContentState,
): DeclarationRender {
  if (state === 'review') return { kind: 'withheld-review' };
  return text ? { kind: 'authored', text } : { kind: 'absent' };
}

/**
 * Whether this product counts toward the "published" KPI: an ACTIVE product whose block serves
 * at least one figure.
 *
 * Deliberately a different measure from `deriveContentState`. Review and withheld states still
 * serve figures (captioned), so they count; a draft product is outside the question entirely
 * and leaves the denominator rather than counting as a failure to publish.
 */
export function countsAsPublished(row: {
  productStatus: string;
  hasBlock: boolean;
  hasFigures: boolean;
}): boolean {
  return row.productStatus === 'Active' && row.hasBlock && row.hasFigures;
}

/** Active products are the denominator; drafts and archived rows are not in the question. */
export function isPublishedDenominator(row: { productStatus: string }): boolean {
  return row.productStatus === 'Active';
}

/** The seven figures, in the order the customer's panel reads them. */
export const FIGURE_FIELDS = [
  { key: 'kcal', label: 'Energy', unit: 'kcal' },
  { key: 'proteinGrams', label: 'Protein', unit: 'g' },
  { key: 'carbsGrams', label: 'Carbs', unit: 'g' },
  { key: 'fatGrams', label: 'Fat', unit: 'g' },
  { key: 'fibreGrams', label: 'Fibre', unit: 'g' },
  { key: 'sugarsGrams', label: 'Sugars', unit: 'g' },
  { key: 'saltGrams', label: 'Salt', unit: 'g' },
] as const;

export type FigureKey = (typeof FIGURE_FIELDS)[number]['key'];

/**
 * A figure input's value on the wire: blank is NULL (not published), never 0.
 *
 * The distinction is the whole point of the field. "0 g salt" is a published claim about the
 * food; an empty box means nobody has measured it. Coercing one into the other publishes a
 * nutrition fact that was never authored.
 */
export function figureToWire(text: string): number | null {
  return text.trim() === '' ? null : Number(text.trim());
}

/** The inverse, for seeding a form from stored values — null renders as an empty box, not "0". */
export function figureToInput(value: number | null | undefined): string {
  return value == null ? '' : String(value);
}

export interface HeatingStep {
  method: string;
  body: string;
}

/**
 * Heating steps to the wire's `heatingJson`.
 *
 * Returns null for an empty list rather than `"[]"`: null is "no heating authored", which the
 * resolver withholds like any other declaration. Rows with neither a method nor a body are
 * dropped, so an empty editing row never becomes a published blank step.
 */
export function heatingToWire(steps: readonly HeatingStep[]): string | null {
  const live = steps
    .map((step) => ({ method: step.method.trim(), body: step.body.trim() }))
    .filter((step) => step.method !== '' || step.body !== '');
  return live.length === 0 ? null : JSON.stringify(live);
}
