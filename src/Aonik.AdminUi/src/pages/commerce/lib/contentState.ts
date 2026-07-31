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
 * Null for an empty list rather than `"[]"`. What null MEANS then differs by target, and the
 * difference is the server's, not this function's:
 *   * a VARIANT stores null and the resolver withholds heating for that combination;
 *   * the default BLOCK coerces null to `"[]"` (UpsertContentAsync), which is an authored
 *     empty panel — the resolver reports heating as NOT withheld.
 * `blockHeatingIsWithheld` below is what keeps the workbench honest about that.
 *
 * Rows with neither half filled are dropped, so an empty editing row never becomes a step.
 */
export function heatingToWire(steps: readonly HeatingStep[]): string | null {
  const live = steps
    .map((step) => ({ method: step.method.trim(), body: step.body.trim() }))
    .filter((step) => step.method !== '' || step.body !== '');
  return live.length === 0 ? null : JSON.stringify(live);
}

/**
 * Rows the operator started but did not finish.
 *
 * `ParseHeatingStrict` requires BOTH a method and a body, so a half-filled row is rejected by
 * the server — and rejected for the whole save, not just that row. Naming them here beats an
 * opaque failure after the fact.
 */
export function incompleteHeatingRows(steps: readonly HeatingStep[]): number[] {
  return steps
    .map((step, index) => ({ step, index }))
    .filter(({ step }) => {
      const method = step.method.trim();
      const body = step.body.trim();
      return (method === '') !== (body === '');
    })
    .map(({ index }) => index + 1);
}

/**
 * Whether a DEFAULT BLOCK's heating is genuinely withheld.
 *
 * It never is. The upsert coerces a null heatingJson to `"[]"`, so a block always has an
 * authored (possibly empty) panel and the resolver reports HeatingWithheld: false. Rendering
 * an empty block panel as "not yet published" would tell the operator that missing
 * preparation guidance is being flagged to customers when an explicitly empty panel is what
 * they actually receive.
 */
export function blockHeatingIsWithheld(): boolean {
  return false;
}

/**
 * The block's AUTHORED fields, as a comparable value.
 *
 * Not `contentVersion`. That column is bumped by the shared content-write pipeline, so
 * creating, editing or retiring any VARIANT increments it while the block's own text is
 * untouched — and the sheet then reported a concurrent block edit, disabled Save, and offered
 * only a Reload that would replace the operator's draft with a server block identical to the
 * one they started from. Losing real work to an unrelated write is worse than the race the
 * check exists for.
 */
export function blockSignature(block: {
  servingLabel: string;
  nutrition: unknown;
  ingredients: string | null;
  allergens: string | null;
  heating: unknown;
}): string {
  return JSON.stringify([
    block.servingLabel,
    block.nutrition,
    block.ingredients,
    block.allergens,
    block.heating,
  ]);
}
