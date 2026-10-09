import { describe, expect, it } from 'vitest';
import type { AdminCollectionItemDto } from '@/types/commerce';
import { addMember, memberState, membershipLines, moveMember, sameMembership, sortOrderFrom } from './merchandisingDraft';

const member = (productId: string, rank: number, overrides: Partial<AdminCollectionItemDto> = {}): AdminCollectionItemDto => ({
  productId, rank, name: productId, slug: productId, status: 'Active', unitPrice: null, currency: null, isPriceable: null, ...overrides,
});

describe('collection membership draft', () => {
  it('moves without changing the loaded rows and submits contiguous full replacement ranks', () => {
    const original = [member('first', 10), member('second', 50), member('third', 100)];
    const moved = moveMember(original, 2, -1);
    expect(membershipLines(moved)).toEqual([
      { productId: 'first', rank: 1 }, { productId: 'third', rank: 2 }, { productId: 'second', rank: 3 },
    ]);
    expect(original.map((item) => item.rank)).toEqual([10, 50, 100]);
    expect(sameMembership(moved, original)).toBe(false);
    expect(sameMembership(moveMember(moved, 1, 1), original)).toBe(true);
  });

  it('keeps drafts and unpriceable products in replacement, and clears only with an explicit empty list', () => {
    expect(membershipLines([member('draft', 5, { status: 'Draft' }), member('no-price', 7, { isPriceable: false })]))
      .toEqual([{ productId: 'draft', rank: 1 }, { productId: 'no-price', rank: 2 }]);
    expect(membershipLines([])).toEqual([]);
    expect(sameMembership([], [member('old', 1)])).toBe(false);
  });

  it('prevents duplicate members and leaves a newly selected product pricing explicitly unknown', () => {
    const original = [member('first', 1)];
    expect(addMember(original, { id: 'first', name: 'First', slug: 'first', status: 'Active' })).toEqual(original);
    const added = addMember(original, { id: 'new', name: 'New', slug: 'new', status: 'Draft' });
    expect(added[1]).toMatchObject({ productId: 'new', rank: 2, isPriceable: null, unitPrice: null, currency: null });
    expect(original).toHaveLength(1);
    expect(moveMember(original, 0, -1)).toEqual(original);
    expect(moveMember(original, 0, 1)).toEqual(original);
  });

  it('distinguishes unavailable pricing, known unpriceable extras, staged drafts and inactive collections', () => {
    expect(memberState(member('unknown', 1), true)).toBe('Price not evaluated');
    expect(memberState(member('no-price', 1, { isPriceable: false }), true)).toBe('Unpriceable — skipped publicly');
    expect(memberState(member('draft', 1, { status: 'Draft' }), true)).toBe('Draft — staged');
    expect(memberState(member('active', 1, { isPriceable: true }), true, false)).toBe('Collection inactive');
    expect(memberState(member('active', 1), null)).toBe('Active — preview unavailable');
  });

  it('validates sort order as the API integer without converting blank or decimals to zero', () => {
    expect(sortOrderFrom(' -15 ')).toBe(-15);
    for (const value of ['', '1.5', '1e2', '2147483648']) expect(() => sortOrderFrom(value)).toThrow();
  });
});
