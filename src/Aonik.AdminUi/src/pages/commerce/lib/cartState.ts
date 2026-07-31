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
 * Two independent causes, both from Spec 068: drift (a line the customer chose is no longer
 * available, or its price moved) and an under-filled box (checkout requires a full box).
 * When both apply the reason names both — the operator would otherwise fix one, retry, and
 * meet the other.
 *
 * A NON-BOX cart returns not-blocked: it has no box rule to violate, and inventing a verdict
 * for a cart this page cannot reason about would be a guess presented as a fact.
 */
export function cartBlocked(boxMeta: CartBoxMetaLike | null | undefined): CartBlockedVerdict {
  if (!boxMeta) return { blocked: false, reason: null };

  const underFilled = boxMeta.filled < boxMeta.size;
  const fill = `${boxMeta.filled} of ${boxMeta.size}`;

  if (boxMeta.drift && underFilled) {
    return {
      blocked: true,
      reason: `A line is unavailable or has been repriced, and the box is under-filled (${fill}).`,
    };
  }
  if (boxMeta.drift) {
    return { blocked: true, reason: 'A line is unavailable or has been repriced.' };
  }
  if (underFilled) {
    return { blocked: true, reason: `The box is under-filled (${fill}).` };
  }
  return { blocked: false, reason: null };
}

/** The compact list-column form: `3/6`, or `—` where there is no box. */
export function formatBoxFill(boxMeta: CartBoxMetaLike | null | undefined): string {
  return boxMeta ? `${boxMeta.filled}/${boxMeta.size}` : '—';
}
