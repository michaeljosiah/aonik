import { describe, expect, it } from 'vitest';

import { normalizeCommercePage } from './commerce';

describe('normalizeCommercePage', () => {
  it('maps the Commerce envelope onto the app shape (page → pageNumber, totalPages computed)', () => {
    const result = normalizeCommercePage({
      items: ['a', 'b', 'c'],
      totalCount: 41,
      page: 2,
      pageSize: 20,
    });

    expect(result).toEqual({
      items: ['a', 'b', 'c'],
      totalCount: 41,
      pageNumber: 2,
      pageSize: 20,
      totalPages: 3, // ceil(41 / 20)
    });
  });

  it('reports at least one page for an empty result set', () => {
    const result = normalizeCommercePage({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    expect(result.totalPages).toBe(1);
    expect(result.pageNumber).toBe(1);
  });

  it('keeps an exact multiple from rounding up an extra page', () => {
    const result = normalizeCommercePage({ items: [], totalCount: 40, page: 1, pageSize: 20 });
    expect(result.totalPages).toBe(2);
  });

  it('survives a zero pageSize without dividing by zero', () => {
    const result = normalizeCommercePage({ items: [], totalCount: 7, page: 1, pageSize: 0 });
    expect(result.totalPages).toBe(7);
    expect(result.pageSize).toBe(0);
  });
});
