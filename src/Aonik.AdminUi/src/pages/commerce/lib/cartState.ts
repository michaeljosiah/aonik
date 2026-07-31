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

/** What the drawer footer may offer for a cart. */
export type CartAction =
  | { kind: 'view-order'; orderId: string; note: string }
  | { kind: 'blocked'; reason: string }
  | { kind: 'resume' }
  | { kind: 'recover' }
  | { kind: 'none' };

/**
 * The single action a cart supports right now.
 *
 * `orderId` is checked FIRST and independently of status, because checkout stamps it while
 * deliberately leaving the cart Open until payment confirms (`CheckoutService.cs:358`; the
 * status only moves to CheckedOut later, at :437). Such a cart is full, undrifted and
 * therefore passes `cartBlocked` — but the service boundary rejects further cart operations
 * because an order has already claimed it. Offering "Resume checkout" there would promise an
 * operation the server refuses, which is the same class of error as ignoring a drift flag.
 */
export function cartAction(cart: {
  status: string;
  orderId: string | null;
  boxMeta: CartBoxMetaLike | null;
}): CartAction {
  if (cart.orderId) {
    return {
      kind: 'view-order',
      orderId: cart.orderId,
      note:
        cart.status === 'Open'
          ? 'An order has claimed this cart and is awaiting payment — the cart itself can no longer be changed.'
          : '',
    };
  }
  if (cart.status === 'Abandoned') return { kind: 'recover' };
  if (cart.status !== 'Open') return { kind: 'none' };

  const verdict = cartBlocked(cart.boxMeta);
  return verdict.blocked ? { kind: 'blocked', reason: verdict.reason! } : { kind: 'resume' };
}

/**
 * Whether a cart is in an ANOMALOUS state, as opposed to merely not finished.
 *
 * `cartBlocked` answers "can this check out right now", which is the correct question for a
 * footer action but the wrong one for an attention list: an under-filled box is the normal
 * state of every live shopping session, so counting it would put a warning on the dashboard
 * whenever customers are actually using the storefront. Drift and over-capacity are different
 * — the customer cannot resolve either by carrying on shopping.
 */
export function cartAnomalous(boxMeta: CartBoxMetaLike | null | undefined): boolean {
  if (!boxMeta) return false;
  return boxMeta.drift || boxMeta.filled > boxMeta.size;
}

/** The compact list-column form: `3/6`, or `—` where there is no box. */
export function formatBoxFill(boxMeta: CartBoxMetaLike | null | undefined): string {
  return boxMeta ? `${boxMeta.filled}/${boxMeta.size}` : '—';
}
