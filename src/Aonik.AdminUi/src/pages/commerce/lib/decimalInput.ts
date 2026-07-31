// Decimal-input validation, parameterised by the column that will store the value.
//
// This exists because the same rule was needed at a fourth call site with a DIFFERENT scale.
// Spec 082 wrote it for `decimal(19,4)` surcharges and Spec 074 reused that; Spec 075's
// nutrition figures are `decimal(9,2)`, so copying it again would have put two nearly-identical
// rules in the tree — and the recurring defect across those specs was a rule applied to some of
// its call sites, not a rule that was wrong.
//
// Everything is decided on the TEXT, never on `Number(text)`. Coercing first loses exactly the
// facts being checked: `1e-5` has no decimal point yet carries five decimals, `''` becomes 0,
// and a value wider than a double is already rounded by the time it is a number. The database
// does not reject an over-precise amount — it ROUNDS it — so a save reports success for a
// figure the store does not hold.

/** Plain fixed-point only. Deliberately excludes `1e-5`, `0x10`, `Infinity` and whitespace. */
const FIXED_POINT = /^-?\d+(\.\d+)?$/;

export interface DecimalRule {
  /** Fractional digits the column stores. A longer value is rounded, not refused. */
  scale: number;
  /** Largest storable magnitude, or null when the significant-digit cap is the only bound. */
  max?: number | null;
  /**
   * Significant digits that survive the trip through a JS number. 15 is the width for which a
   * decimal literal is the shortest string that round-trips through a double, so anything
   * accepted is transmitted exactly as typed.
   */
  maxSignificantDigits?: number;
  /** How the subject is named in messages, e.g. "A surcharge" or "Energy". */
  subject: string;
}

/**
 * Returns a message, or null when the text is storable exactly as typed.
 *
 * A BLANK value is null (not published / no amount) and is the caller's business — callers
 * that treat blank as meaningful must check it before calling, because `Number('')` is 0 and
 * silently authors a zero.
 */
export function validateDecimalInput(text: string, rule: DecimalRule): string | null {
  const trimmed = text.trim();
  if (trimmed === '') return null;

  if (!FIXED_POINT.test(trimmed)) {
    return `${rule.subject} must be a plain number, like 2.50 — exponent notation is not stored exactly.`;
  }
  if (trimmed.startsWith('-')) return `${rule.subject} cannot be negative.`;

  const [whole, fraction = ''] = trimmed.split('.');
  if (fraction.length > rule.scale) {
    return `${rule.subject} is stored to ${rule.scale} decimal place${
      rule.scale === 1 ? '' : 's'
    } — a longer value would be rounded on save.`;
  }

  const significant = (whole + fraction).replace(/^0+/, '').length;
  if (significant > (rule.maxSignificantDigits ?? 15)) {
    return `${rule.subject} has more digits than can be sent exactly — use at most ${
      rule.maxSignificantDigits ?? 15
    }.`;
  }

  if (rule.max != null && Number(trimmed) > rule.max) {
    return `${rule.subject} is larger than the stored maximum (${rule.max}).`;
  }
  return null;
}

/**
 * Nutrition figures: `decimal(9,2)` with the service's own bound
 * (ProductContentService.FigureBound). Negative and over-bound are V-C7 server-side; catching
 * them here keeps a rejected panel from being reported as saved.
 */
export const NUTRITION_FIGURE_RULE = { scale: 2, max: 9_999_999.99, subject: 'A figure' } as const;
