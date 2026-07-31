// Cart blocked-state derivation (Spec 083 §4). Pure, so the column, the drawer banner and
// the footer action are three renderings of ONE verdict rather than three re-derivations
// that can disagree — the UI must never offer an operation the Spec 068 rules block.

/** The shape of `AdminCartBoxMetaDto`, restated so this module stays free of DTO imports. */
export interface CartBoxMetaLike {
  size: number;
  filled: number;
  /** Computed server-side at load: a line is unavailable or an add-on price moved. */
  drift: boolean;
}

export interface CartBlockedVerdict {
  blocked: boolean;
  /** Null exactly when `blocked` is false — the caller renders nothing rather than "OK". */
  reason: string | null;
}

/**
 * Whether checkout is blocked for this cart right now, and why.
 *
 * Two independent causes, both from Spec 068:
 *
 *   * FILL. The server gate is `units != BoxSize` (BoxCartService.cs:416 and :1148) — an
 *     EXACT match, not a minimum. An over-filled box is blocked exactly as an under-filled
 *     one is, and the server carries its own "remove N" message for it, so treating 7/6 as
 *     "full" would enable a resume the rules reject.
 *
 *   * DRIFT. Reported without attributing a cause, because `ComputeCartStatesAsync` raises it
 *     for container-level changes too — the box product going inactive, its kind or pricing
 *     mode changing, the chosen size falling outside the current plan — none of which involve
 *     a line at all. Naming lines would send the operator to inspect rows that are fine.
 *
 * When both apply the reason names both: the operator would otherwise fix one, retry, and
 * meet the other.
 *
 * A NON-BOX cart returns not-blocked: it has no box rule to violate, and inventing a verdict
 * for a cart this page cannot reason about would be a guess presented as a fact.
 */
export function cartBlocked(boxMeta: CartBoxMetaLike | null | undefined): CartBlockedVerdict {
  if (!boxMeta) return { blocked: false, reason: null };

  const shortfall = boxMeta.size - boxMeta.filled;
  const fill = `${boxMeta.filled} of ${boxMeta.size}`;
  const fillReason =
    shortfall > 0
      ? `the box is under-filled (${fill})`
      : shortfall < 0
        ? `the box holds more than its size (${fill}) and ${-shortfall} must be removed`
        : null;
  const driftReason = boxMeta.drift
    ? 'something in this cart has changed since it was built'
    : null;

  const causes = [driftReason, fillReason].filter(Boolean) as string[];
  if (causes.length === 0) return { blocked: false, reason: null };

  const joined = causes.join(', and ');
  return { blocked: true, reason: `${joined.charAt(0).toUpperCase()}${joined.slice(1)}.` };
}

/** The compact list-column form: `3/6`, or `—` where there is no box. */
export function formatBoxFill(boxMeta: CartBoxMetaLike | null | undefined): string {
  return boxMeta ? `${boxMeta.filled}/${boxMeta.size}` : '—';
}
