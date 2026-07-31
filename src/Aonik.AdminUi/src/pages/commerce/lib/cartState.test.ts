import { describe, expect, it } from 'vitest';

import { cartAction, cartBlocked, formatBoxFill } from './cartState';

describe('cartBlocked', () => {
  it('blocks a drifted box WITHOUT blaming a line', () => {
    // ComputeCartStatesAsync raises drift for container-level changes too (the box product
    // going inactive, its size falling outside the plan). Naming lines would send the
    // operator to inspect rows that are perfectly fine.
    const verdict = cartBlocked({ size: 6, filled: 6, drift: true });
    expect(verdict.blocked).toBe(true);
    expect(verdict.reason).toMatch(/changed since it was built/);
    expect(verdict.reason).not.toMatch(/line/i);
  });

  it('blocks an under-filled box and names the shortfall', () => {
    const verdict = cartBlocked({ size: 6, filled: 3, drift: false });
    expect(verdict.blocked).toBe(true);
    expect(verdict.reason).toBe('The box is under-filled (3 of 6).');
  });

  it('names BOTH causes when both apply', () => {
    // Reporting only one would have the operator fix it, retry, and meet the other.
    const verdict = cartBlocked({ size: 6, filled: 2, drift: true });
    expect(verdict.blocked).toBe(true);
    expect(verdict.reason).toMatch(/changed since it was built, and the box is under-filled \(2 of 6\)/);
  });

  it('does not block a full, undrifted box', () => {
    expect(cartBlocked({ size: 6, filled: 6, drift: false })).toEqual({
      blocked: false,
      reason: null,
    });
  });

  it('does not block a NON-box cart — it has no box rule to violate', () => {
    // Inventing a verdict for a cart this page cannot reason about would be a guess.
    expect(cartBlocked(null)).toEqual({ blocked: false, reason: null });
    expect(cartBlocked(undefined)).toEqual({ blocked: false, reason: null });
  });

  it('carries a reason exactly when blocked', () => {
    const cases = [
      { size: 6, filled: 6, drift: false },
      { size: 6, filled: 1, drift: false },
      { size: 6, filled: 6, drift: true },
      { size: 6, filled: 1, drift: true },
    ];
    for (const boxMeta of cases) {
      const verdict = cartBlocked(boxMeta);
      expect(verdict.reason === null).toBe(!verdict.blocked);
    }
  });

  it('BLOCKS an over-filled box — the server gate is equality, not a minimum', () => {
    // BoxCartService.cs:416 and :1148 both test `units != BoxSize`, and the server carries
    // its own "remove N" message, so 7/6 is blocked exactly as 5/6 is. Treating it as full
    // would offer a resume the rules reject.
    const verdict = cartBlocked({ size: 6, filled: 7, drift: false });
    expect(verdict.blocked).toBe(true);
    expect(verdict.reason).toMatch(/holds more than its size \(7 of 6\) and 1 must be removed/);
  });
});

describe('formatBoxFill', () => {
  it('renders the fill for a box cart and a dash otherwise', () => {
    expect(formatBoxFill({ size: 6, filled: 3, drift: false })).toBe('3/6');
    expect(formatBoxFill(null)).toBe('—');
  });
});

describe('cartAction', () => {
  const full = { size: 6, filled: 6, drift: false };

  it('directs a CLAIMED open cart to its order instead of offering resume', () => {
    // Checkout stamps OrderId while leaving the cart Open until payment confirms
    // (CheckoutService.cs:358 vs :437). The cart is full and undrifted, so cartBlocked passes
    // it — but the service boundary rejects further cart operations once an order claims it.
    const action = cartAction({ status: 'Open', orderId: 'order-1', boxMeta: full });
    expect(action.kind).toBe('view-order');
    expect(action.kind === 'view-order' && action.note).toMatch(/awaiting payment/);
  });

  it('offers resume only for an unclaimed, unblocked open cart', () => {
    expect(cartAction({ status: 'Open', orderId: null, boxMeta: full }).kind).toBe('resume');
  });

  it('blocks an open cart that cannot check out, carrying the reason', () => {
    const action = cartAction({
      status: 'Open',
      orderId: null,
      boxMeta: { size: 6, filled: 4, drift: false },
    });
    expect(action.kind).toBe('blocked');
    expect(action.kind === 'blocked' && action.reason).toMatch(/under-filled/);
  });

  it('offers recovery for an abandoned cart', () => {
    expect(cartAction({ status: 'Abandoned', orderId: null, boxMeta: null }).kind).toBe('recover');
  });

  it('links a checked-out cart to its order', () => {
    expect(cartAction({ status: 'CheckedOut', orderId: 'order-9', boxMeta: null }).kind).toBe(
      'view-order',
    );
  });

  it('offers nothing for a cart in a status this page has no action for', () => {
    // Expired, or any status added server-side later — silence beats a guessed action.
    expect(cartAction({ status: 'Expired', orderId: null, boxMeta: null }).kind).toBe('none');
  });
});
