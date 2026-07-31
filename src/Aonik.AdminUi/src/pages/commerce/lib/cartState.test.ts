import { describe, expect, it } from 'vitest';

import { cartBlocked, formatBoxFill } from './cartState';

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
