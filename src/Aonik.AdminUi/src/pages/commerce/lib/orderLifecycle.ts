// Order lifecycle derivation for the Spec 083 drawer stepper. Pure and tested, because what
// a stepper CLAIMS about an order is exactly as load-bearing as what the list claims.
//
// Spec 083 §2 names the spine Created→Invoiced→Funded→Paid→Fulfilled. Two of those are not
// rendered as their own steps, and one is rendered as untracked, because the storefront
// projection cannot evidence them:
//
//   * Invoiced and Funded — the DTO carries no invoice or funding marker distinct from the
//     order existing and the payment status. Two always-complete pills would be decoration
//     that reads as verified fact.
//   * Fulfilled — `DeriveFulfilment` (AdminStorefrontService.cs:731) returns only
//     "Unfulfilled" or "Cancelled"; there is no Fulfilled value to reach. Rendering it as a
//     pending step would tell the operator delivery is being tracked and is merely
//     outstanding. It is not tracked at all — Spec 069 phase 2 is where that lands.

export type LifecycleStepState = 'done' | 'current' | 'pending' | 'untracked';

export interface LifecycleStep {
  key: string;
  label: string;
  state: LifecycleStepState;
  /** Shown under an `untracked` step so the gap is explained rather than merely greyed. */
  note?: string;
}

export interface OrderLifecycle {
  steps: LifecycleStep[];
  /** Set when the order stopped rather than progressed — cancelled, failed or expired. */
  halted: { label: string; reason: string } | null;
}

/** Spine statuses that end the order without completing it. */
const HALTED: Record<string, string> = {
  Cancelled: 'This order was cancelled.',
  Failed: 'This order failed.',
  Expired: 'This order expired before it completed.',
};

const CAPTURED = 'Captured';
const COMPLETE = 'Complete';

export function orderLifecycle(input: {
  orderStatus: string;
  paymentStatus: string;
}): OrderLifecycle {
  const haltReason = HALTED[input.orderStatus];
  const paid = input.paymentStatus === CAPTURED;
  const complete = input.orderStatus === COMPLETE;

  if (haltReason) {
    return {
      // The order was created and may well have been paid before it stopped; both stay
      // truthful. What follows is not "pending" — it is never happening.
      steps: [
        { key: 'created', label: 'Created', state: 'done' },
        { key: 'paid', label: 'Paid', state: paid ? 'done' : 'pending' },
      ],
      halted: { label: input.orderStatus, reason: haltReason },
    };
  }

  return {
    steps: [
      { key: 'created', label: 'Created', state: 'done' },
      { key: 'paid', label: 'Paid', state: paid ? 'done' : 'current' },
      {
        key: 'complete',
        label: 'Completed',
        state: complete ? 'done' : paid ? 'current' : 'pending',
      },
      {
        key: 'fulfilled',
        label: 'Fulfilled',
        state: 'untracked',
        note: 'Fulfilment is not tracked yet',
      },
    ],
    halted: null,
  };
}
