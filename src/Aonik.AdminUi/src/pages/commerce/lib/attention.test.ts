import { describe, expect, it } from 'vitest';

import { allSettled, buildAttentionRows, type AttentionSources } from './attention';

const loading: AttentionSources = {
  contentReview: { kind: 'loading' },
  deliveryPromise: { kind: 'loading' },
  stagedDrafts: { kind: 'loading' },
  skippedExtras: { kind: 'loading' },
  abandonedCarts: { kind: 'loading' },
  blockedCarts: { kind: 'loading' },
};

const quiet: AttentionSources = {
  contentReview: { kind: 'ready', value: { count: 0, inspected: 10, complete: true } },
  deliveryPromise: { kind: 'ready', value: '2026-08-04' },
  stagedDrafts: { kind: 'ready', value: { count: 0, collectionsInspected: 3, complete: true } },
  skippedExtras: { kind: 'ready', value: 0 },
  abandonedCarts: { kind: 'ready', value: 0 },
  blockedCarts: { kind: 'ready', value: { count: 0, window: 25 } },
};

function keys(sources: AttentionSources) {
  return buildAttentionRows(sources).map((row) => row.key);
}

describe('buildAttentionRows', () => {
  it('OMITS a source with nothing to report rather than reporting zero', () => {
    // "0 products awaiting review" trains operators to skim past the card.
    const rows = buildAttentionRows({ ...quiet, deliveryPromise: { kind: 'ready', value: null } });
    expect(rows.map((r) => r.key)).toEqual(['delivery']);
  });

  it('contributes nothing while a source is still loading', () => {
    expect(buildAttentionRows(loading)).toEqual([]);
    expect(allSettled(loading)).toBe(false);
    expect(allSettled(quiet)).toBe(true);
  });

  it('SHOWS an unavailable source instead of staying silent', () => {
    // Silence would read as "nothing to report", which a failed read cannot establish.
    const rows = buildAttentionRows({ ...quiet, contentReview: { kind: 'unavailable' } });
    const row = rows.find((r) => r.key === 'content');
    expect(row).toBeDefined();
    expect(row!.statement).toMatch(/could not be read/);
    expect(row!.subline).toMatch(/unknown, not clear/);
    expect(row!.tone).toBe('muted');
  });

  it('warns loudly when no delivery promise is published', () => {
    const rows = buildAttentionRows({ ...quiet, deliveryPromise: { kind: 'ready', value: null } });
    expect(rows[0].tone).toBe('warn');
    expect(rows[0].statement).toMatch(/customers see no date/i);
  });

  it('keeps a live promise as an info row rather than hiding it', () => {
    const rows = buildAttentionRows(quiet);
    expect(rows).toHaveLength(1);
    expect(rows[0].key).toBe('delivery');
    expect(rows[0].tone).toBe('info');
  });

  it('tones each finding by urgency', () => {
    const rows = buildAttentionRows({
      ...quiet,
      contentReview: { kind: 'ready', value: { count: 3, inspected: 10, complete: true } },
      skippedExtras: { kind: 'ready', value: 1 },
      stagedDrafts: { kind: 'ready', value: { count: 2, collectionsInspected: 4, complete: true } },
      abandonedCarts: { kind: 'ready', value: 9 },
    });
    const toneOf = (key: string) => rows.find((r) => r.key === key)?.tone;
    expect(toneOf('content')).toBe('warn');
    expect(toneOf('extras')).toBe('warn');
    expect(toneOf('drafts')).toBe('muted');
    expect(toneOf('abandoned-carts')).toBe('muted');
  });

  it('names the window a cart count was taken over', () => {
    const rows = buildAttentionRows({
      ...quiet,
      blockedCarts: { kind: 'ready', value: { count: 2, window: 25 } },
    });
    expect(rows.find((r) => r.key === 'blocked-carts')?.subline).toMatch(/25 most recent/);
  });

  it('discloses a partial draft count instead of presenting it as complete', () => {
    const partial = buildAttentionRows({
      ...quiet,
      stagedDrafts: { kind: 'ready', value: { count: 5, collectionsInspected: 20, complete: false } },
    }).find((r) => r.key === 'drafts');
    expect(partial?.subline).toMatch(/first 20 collections only/);

    const full = buildAttentionRows({
      ...quiet,
      stagedDrafts: { kind: 'ready', value: { count: 5, collectionsInspected: 4, complete: true } },
    }).find((r) => r.key === 'drafts');
    expect(full?.subline).not.toMatch(/only/);
  });

  it('reports a partial content scan as a FLOOR, not a total', () => {
    const row = buildAttentionRows({
      ...quiet,
      contentReview: { kind: 'ready', value: { count: 7, inspected: 200, complete: false } },
    }).find((r) => r.key === 'content');
    expect(row?.statement).toMatch(/^At least 7 products/);
    expect(row?.subline).toMatch(/first 200 products only/);
  });

  it('singularises counts', () => {
    const rows = buildAttentionRows({
      ...quiet,
      contentReview: { kind: 'ready', value: { count: 1, inspected: 10, complete: true } },
      skippedExtras: { kind: 'ready', value: 1 },
    });
    expect(rows.find((r) => r.key === 'content')?.statement).toBe('1 product awaiting content review');
    expect(rows.find((r) => r.key === 'extras')?.statement).toBe(
      '1 extra skipped for want of a price',
    );
  });

  it('orders what is wrong before what is merely worth knowing', () => {
    const ordered = keys({
      ...quiet,
      contentReview: { kind: 'ready', value: { count: 1, inspected: 10, complete: true } },
      deliveryPromise: { kind: 'ready', value: null },
      stagedDrafts: { kind: 'ready', value: { count: 1, collectionsInspected: 2, complete: true } },
      skippedExtras: { kind: 'ready', value: 1 },
      blockedCarts: { kind: 'ready', value: { count: 1, window: 25 } },
      abandonedCarts: { kind: 'ready', value: 1 },
    });
    expect(ordered).toEqual([
      'content',
      'delivery',
      'drafts',
      'extras',
      'blocked-carts',
      'abandoned-carts',
    ]);
  });

  it('gives every row a link to the surface that owns the state', () => {
    const rows = buildAttentionRows({
      ...quiet,
      contentReview: { kind: 'ready', value: { count: 1, inspected: 10, complete: true } },
      skippedExtras: { kind: 'ready', value: 1 },
      abandonedCarts: { kind: 'ready', value: 1 },
    });
    for (const row of rows) {
      expect(row.href).toMatch(/^\/commerce\//);
    }
  });
});

describe('clean but PARTIAL scans', () => {
  it('reports a zero content count that could not see every product', () => {
    // 200-product scan of a 300-product store: a flagged product 201 would otherwise render
    // identically to a genuinely clean store. Zero + incomplete is not "nothing to report".
    const row = buildAttentionRows({
      ...quiet,
      contentReview: { kind: 'ready', value: { count: 0, inspected: 200, complete: false } },
    }).find((r) => r.key === 'content');
    expect(row).toBeDefined();
    expect(row!.statement).toMatch(/checked as far as the first 200 products/);
    expect(row!.subline).toMatch(/rest was not inspected/);
    expect(row!.tone).toBe('muted');
  });

  it('reports a zero draft count that could not see every collection', () => {
    const row = buildAttentionRows({
      ...quiet,
      stagedDrafts: { kind: 'ready', value: { count: 0, collectionsInspected: 20, complete: false } },
    }).find((r) => r.key === 'drafts');
    expect(row?.statement).toMatch(/checked as far as the first 20 collections/);
  });

  it('still omits a zero count when the scan WAS complete', () => {
    expect(
      buildAttentionRows({
        ...quiet,
        contentReview: { kind: 'ready', value: { count: 0, inspected: 12, complete: true } },
        stagedDrafts: { kind: 'ready', value: { count: 0, collectionsInspected: 3, complete: true } },
      }).map((r) => r.key),
    ).toEqual(['delivery']);
  });
});

describe('cart sources degrade like every other source', () => {
  it('shows an unavailable row when the open-cart read fails', () => {
    const row = buildAttentionRows({
      ...quiet,
      blockedCarts: { kind: 'unavailable' },
    }).find((r) => r.key === 'blocked-carts');
    expect(row?.statement).toMatch(/could not be read/);
  });

  it('shows an unavailable row when the abandoned-cart read fails', () => {
    // Without this, a carts outage was indistinguishable from having no abandoned carts.
    const row = buildAttentionRows({
      ...quiet,
      abandonedCarts: { kind: 'unavailable' },
    }).find((r) => r.key === 'abandoned-carts');
    expect(row?.statement).toMatch(/could not be read/);
  });
});
