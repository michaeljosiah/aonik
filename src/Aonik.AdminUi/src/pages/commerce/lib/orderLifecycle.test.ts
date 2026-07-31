import { describe, expect, it } from 'vitest';

import { orderLifecycle } from './orderLifecycle';

function stateOf(orderStatus: string, paymentStatus: string, key: string) {
  return orderLifecycle({ orderStatus, paymentStatus }).steps.find((s) => s.key === key)?.state;
}

describe('orderLifecycle', () => {
  it('never claims fulfilment progress — the projection has no Fulfilled value', () => {
    // DeriveFulfilment returns only Unfulfilled or Cancelled, so a "pending" fulfilment step
    // would tell the operator delivery is tracked and merely outstanding. It is not tracked.
    const step = orderLifecycle({ orderStatus: 'Complete', paymentStatus: 'Captured' }).steps.find(
      (s) => s.key === 'fulfilled',
    );
    expect(step?.state).toBe('untracked');
    expect(step?.note).toMatch(/not tracked/i);
  });

  it('marks payment done only on Captured', () => {
    expect(stateOf('Pending', 'Captured', 'paid')).toBe('done');
    expect(stateOf('Pending', 'Pending', 'paid')).toBe('current');
    expect(stateOf('Pending', 'RequiresAction', 'paid')).toBe('current');
  });

  it('advances completion only on the spine Complete status', () => {
    expect(stateOf('Complete', 'Captured', 'complete')).toBe('done');
    expect(stateOf('Transmitted', 'Captured', 'complete')).toBe('current');
    expect(stateOf('Pending', 'Pending', 'complete')).toBe('pending');
  });

  it('halts on cancelled, failed and expired rather than showing pending steps', () => {
    for (const status of ['Cancelled', 'Failed', 'Expired']) {
      const lifecycle = orderLifecycle({ orderStatus: status, paymentStatus: 'Pending' });
      expect(lifecycle.halted?.label).toBe(status);
      expect(lifecycle.halted?.reason).toBeTruthy();
      // What follows a halt is not "pending" — it is never happening, so it is not rendered.
      expect(lifecycle.steps.map((s) => s.key)).toEqual(['created', 'paid']);
    }
  });

  it('keeps a capture that happened before the halt truthful', () => {
    // A cancelled order may well have been paid first; erasing that would misreport money.
    const lifecycle = orderLifecycle({ orderStatus: 'Cancelled', paymentStatus: 'Captured' });
    expect(lifecycle.steps.find((s) => s.key === 'paid')?.state).toBe('done');
  });

  it('treats an unknown spine status as in-flight rather than halted', () => {
    // OrderStatus is an open string; an unrecognised one must not be reported as cancelled.
    const lifecycle = orderLifecycle({ orderStatus: 'SomeNewStatus', paymentStatus: 'Captured' });
    expect(lifecycle.halted).toBeNull();
    expect(lifecycle.steps).toHaveLength(4);
  });
});
