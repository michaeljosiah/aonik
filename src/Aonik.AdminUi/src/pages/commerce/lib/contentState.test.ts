import { describe, expect, it } from 'vitest';

import {
  blockSignature,
  countsAsPublished,
  deriveContentState,
  figureToInput,
  figureToWire,
  heatingToWire,
  isPublishedDenominator,
  renderDeclaration,
} from './contentState';

const authored = { ingredients: 'Rice, tomato', allergens: 'None' };
const declarationless = { ingredients: null, allergens: null };

describe('deriveContentState', () => {
  it('maps a missing block to none', () => {
    expect(deriveContentState(null, false)).toBe('none');
    expect(deriveContentState(undefined, false)).toBe('none');
  });

  it('NEVER maps a figure-less block to none — a block is a block', () => {
    // The resolver may be serving this block's declarations right now. Calling it "nothing
    // published" would offer an authoring CTA that overwrites live safety content.
    expect(deriveContentState(authored, false)).toBe('authored');
  });

  it('maps ANY server staleness to review, not just the explicit flag', () => {
    // isStale covers requiresReview OR a binding that no longer matches current defaults. The
    // resolver withholds in both cases, so neither may be labelled Authored.
    expect(deriveContentState(authored, true)).toBe('review');
    expect(deriveContentState(declarationless, true)).toBe('review');
  });

  it('ranks staleness ABOVE missing declarations', () => {
    expect(deriveContentState(declarationless, true)).toBe('review');
  });

  it('maps a block with no declarations to withheld', () => {
    expect(deriveContentState(declarationless, false)).toBe('withheld');
  });

  it('treats one authored declaration as authored', () => {
    expect(deriveContentState({ ingredients: 'Rice', allergens: null }, false)).toBe('authored');
    expect(deriveContentState({ ingredients: null, allergens: 'Celery' }, false)).toBe('authored');
  });
});

describe('renderDeclaration', () => {
  it('WITHHOLDS authored text while under review', () => {
    // Showing it would tell the operator their allergen line is live when the resolver is
    // withholding it from customers.
    expect(renderDeclaration('Contains celery', 'review')).toEqual({ kind: 'withheld-review' });
  });

  it('distinguishes absent from withheld', () => {
    expect(renderDeclaration(null, 'withheld')).toEqual({ kind: 'absent' });
    expect(renderDeclaration(null, 'review')).toEqual({ kind: 'withheld-review' });
  });

  it('renders authored text when nothing is withholding it', () => {
    expect(renderDeclaration('Contains celery', 'authored')).toEqual({
      kind: 'authored',
      text: 'Contains celery',
    });
  });
});

describe('published KPI', () => {
  const row = (over: Partial<Parameters<typeof countsAsPublished>[0]> = {}) => ({
    productStatus: 'Active',
    hasBlock: true,
    hasFigures: true,
    ...over,
  });

  it('counts an active product whose block serves figures', () => {
    expect(countsAsPublished(row())).toBe(true);
  });

  it('still counts a product under review or withholding declarations', () => {
    // Both states serve figures, captioned — this measure is about figures, not declarations,
    // and is deliberately not the same question as deriveContentState.
    expect(countsAsPublished(row())).toBe(true);
  });

  it('does not count a block with no figures', () => {
    expect(countsAsPublished(row({ hasFigures: false }))).toBe(false);
  });

  it('leaves a DRAFT product out of both numerator and denominator', () => {
    expect(countsAsPublished(row({ productStatus: 'Draft' }))).toBe(false);
    expect(isPublishedDenominator({ productStatus: 'Draft' })).toBe(false);
    expect(isPublishedDenominator({ productStatus: 'Active' })).toBe(true);
  });
});

describe('figure round-trip', () => {
  it('sends BLANK as null, never 0', () => {
    // "0 g salt" is a published claim about the food; an empty box means nobody measured it.
    expect(figureToWire('')).toBeNull();
    expect(figureToWire('   ')).toBeNull();
  });

  it('sends an authored zero as 0', () => {
    expect(figureToWire('0')).toBe(0);
  });

  it('renders null as an empty box, not "0"', () => {
    expect(figureToInput(null)).toBe('');
    expect(figureToInput(undefined)).toBe('');
    expect(figureToInput(0)).toBe('0');
    expect(figureToInput(12.5)).toBe('12.5');
  });
});

describe('heatingToWire', () => {
  it('returns null for no steps, so heating withholds like any other declaration', () => {
    expect(heatingToWire([])).toBeNull();
    expect(heatingToWire([{ method: '  ', body: '' }])).toBeNull();
  });

  it('drops empty editing rows rather than publishing blank steps', () => {
    const json = heatingToWire([
      { method: 'Oven', body: '20 minutes at 180C' },
      { method: '', body: '' },
    ]);
    expect(JSON.parse(json!)).toEqual([{ method: 'Oven', body: '20 minutes at 180C' }]);
  });

  it('keeps a row with only one side filled, because it is partially authored', () => {
    const json = heatingToWire([{ method: 'Microwave', body: '' }]);
    expect(JSON.parse(json!)).toEqual([{ method: 'Microwave', body: '' }]);
  });

  it('trims', () => {
    const json = heatingToWire([{ method: '  Oven  ', body: '  hot  ' }]);
    expect(JSON.parse(json!)).toEqual([{ method: 'Oven', body: 'hot' }]);
  });
});

describe('blockSignature', () => {
  const block = {
    servingLabel: 'Per 350 g',
    nutrition: { kcal: 520, proteinGrams: null },
    ingredients: 'Rice',
    allergens: null,
    heating: [{ method: 'Oven', body: '20 min' }],
  };

  it('is unchanged by anything outside the authored fields', () => {
    // contentVersion moves when a VARIANT is written, because the write pipeline is shared —
    // so versioning the row reported a block edit that never happened and offered a reload
    // that would discard the operator's draft.
    expect(blockSignature({ ...block, contentVersion: 9 } as never)).toBe(blockSignature(block));
  });

  it('changes when any authored field changes', () => {
    expect(blockSignature({ ...block, allergens: 'Celery' })).not.toBe(blockSignature(block));
    expect(blockSignature({ ...block, servingLabel: 'Per 1' })).not.toBe(blockSignature(block));
    expect(blockSignature({ ...block, heating: [] })).not.toBe(blockSignature(block));
    expect(blockSignature({ ...block, nutrition: { kcal: 521, proteinGrams: null } })).not.toBe(
      blockSignature(block),
    );
  });

  it('distinguishes a withheld declaration from an empty one', () => {
    expect(blockSignature({ ...block, ingredients: '' })).not.toBe(
      blockSignature({ ...block, ingredients: null }),
    );
  });
});
