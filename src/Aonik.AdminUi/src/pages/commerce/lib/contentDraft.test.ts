import { describe, expect, it } from 'vitest';

import type { ProductContentDto, ProductContentVariantDto } from '@/types/commerce';

import {
  draftFromBlock,
  draftFromVariant,
  emptyDraft,
  validateDraft,
  wireFromDraft,
} from './contentDraft';

const block = {
  productId: 'p1',
  servingLabel: 'Per 350 g',
  nutrition: {
    kcal: 520,
    proteinGrams: 24.5,
    carbsGrams: null,
    fatGrams: 0,
    fibreGrams: null,
    sugarsGrams: null,
    saltGrams: null,
  },
  ingredients: 'Rice, tomato',
  allergens: null,
  heating: [{ method: 'Oven', body: '20 min at 180C' }],
  describesSelectionJson: '{}',
  requiresReview: false,
  contentVersion: 3,
} as ProductContentDto;

describe('draftFromBlock', () => {
  it('renders a null figure as an EMPTY box and a stored zero as "0"', () => {
    // The distinction the whole form rests on: an empty box is "not published"; 0 is a
    // published claim that the food contains none of it.
    const draft = draftFromBlock(block);
    expect(draft.figures.carbsGrams).toBe('');
    expect(draft.figures.fatGrams).toBe('0');
    expect(draft.figures.kcal).toBe('520');
  });

  it('renders a null declaration as an empty field, not the word null', () => {
    expect(draftFromBlock(block).allergens).toBe('');
    expect(draftFromBlock(block).ingredients).toBe('Rice, tomato');
  });

  it('returns an empty draft for a product with no block', () => {
    expect(draftFromBlock(null)).toEqual(emptyDraft());
  });
});

describe('draftFromVariant', () => {
  it('handles a variant whose heating is null', () => {
    const variant = {
      id: 'v1',
      productId: 'p1',
      selectionJson: '{"spice":"hot"}',
      servingLabel: 'Per 350 g',
      nutrition: block.nutrition,
      ingredients: null,
      allergens: 'Celery',
      heating: null,
      isActive: true,
    } as ProductContentVariantDto;
    expect(draftFromVariant(variant).heating).toEqual([]);
    expect(draftFromVariant(variant).allergens).toBe('Celery');
  });
});

describe('wireFromDraft', () => {
  it('sends a blank figure as NULL and an authored zero as 0', () => {
    const draft = draftFromBlock(block);
    const wire = wireFromDraft(draft);
    expect(wire.carbsGrams).toBeNull();
    expect(wire.fatGrams).toBe(0);
    expect(wire.kcal).toBe(520);
  });

  it('sends blank declarations as NULL, not empty strings', () => {
    // "" would be an authored empty declaration; null is withheld. The resolver treats them
    // very differently, and only one of them is honest about an unauthored field.
    const wire = wireFromDraft({ ...emptyDraft(), servingLabel: 'x' });
    expect(wire.ingredients).toBeNull();
    expect(wire.allergens).toBeNull();
    expect(wire.heatingJson).toBeNull();
  });

  it('trims text', () => {
    const wire = wireFromDraft({ ...emptyDraft(), servingLabel: ' Per 1 ', ingredients: '  Rice  ' });
    expect(wire.servingLabel).toBe('Per 1');
    expect(wire.ingredients).toBe('Rice');
  });

  it('carries every one of the seven figures', () => {
    const wire = wireFromDraft({ ...emptyDraft(), servingLabel: 'x' });
    for (const key of [
      'kcal',
      'proteinGrams',
      'carbsGrams',
      'fatGrams',
      'fibreGrams',
      'sugarsGrams',
      'saltGrams',
    ]) {
      expect(key in wire).toBe(true);
    }
  });
});

describe('validateDraft', () => {
  it('requires a serving label, because it captions every figure', () => {
    expect(validateDraft(emptyDraft())).toMatch(/serving label/i);
  });

  it('rejects a figure beyond the column scale, naming the field', () => {
    // decimal(9,2) rounds a third decimal rather than refusing it, so the save would report
    // success for a panel the store does not hold.
    const draft = { ...emptyDraft(), servingLabel: 'Per 1' };
    draft.figures.proteinGrams = '24.567';
    expect(validateDraft(draft)).toMatch(/^Protein is stored to 2 decimal places/);
  });

  it('rejects a negative figure', () => {
    const draft = { ...emptyDraft(), servingLabel: 'Per 1' };
    draft.figures.kcal = '-5';
    expect(validateDraft(draft)).toMatch(/Energy cannot be negative/);
  });

  it('rejects a figure past the storable bound', () => {
    const draft = { ...emptyDraft(), servingLabel: 'Per 1' };
    draft.figures.saltGrams = '10000000';
    expect(validateDraft(draft)).toMatch(/Salt is larger than the stored maximum/);
  });

  it('accepts an all-blank panel — a declarations-only block is legitimate', () => {
    expect(validateDraft({ ...emptyDraft(), servingLabel: 'Per 1', allergens: 'Celery' })).toBeNull();
  });
});

describe('authored-empty heating', () => {
  const variantWith = (heating: { method: string; body: string }[] | null) =>
    ({
      id: 'v1',
      productId: 'p1',
      selectionJson: '{}',
      servingLabel: 'Per 1',
      nutrition: block.nutrition,
      ingredients: null,
      allergens: null,
      heating,
      isActive: true,
    }) as ProductContentVariantDto;

  it('resends [] for a variant that authored an EMPTY panel', () => {
    // `[]` resolves with heatingWithheld false; `null` with true. Collapsing them meant
    // editing an unrelated figure silently converted "no heating required" into "withheld".
    const draft = draftFromVariant(variantWith([]));
    expect(draft.heatingAuthoredEmpty).toBe(true);
    expect(wireFromDraft(draft).heatingJson).toBe('[]');
  });

  it('sends null for a variant whose heating was withheld', () => {
    const draft = draftFromVariant(variantWith(null));
    expect(draft.heatingAuthoredEmpty).toBe(false);
    expect(wireFromDraft(draft).heatingJson).toBeNull();
  });

  it('sends real steps once any are added, whatever the source state', () => {
    const draft = draftFromVariant(variantWith([]));
    draft.heating = [{ method: 'Oven', body: 'hot' }];
    expect(JSON.parse(wireFromDraft(draft).heatingJson!)).toEqual([
      { method: 'Oven', body: 'hot' },
    ]);
  });
});

describe('unreadable heating', () => {
  const damaged = { ...block, heating: null } as ProductContentDto;

  it('refuses to save until the operator decides', () => {
    // The resolver withholds unparseable heating; the admin read used to report it as an empty
    // panel, so editing a figure silently republished it as "no heating required".
    const draft = draftFromBlock(damaged);
    expect(draft.heatingUnreadable).toBe(true);
    expect(validateDraft(draft)).toMatch(/cannot be read/);
  });

  it('saves once the replacement is accepted', () => {
    const draft = { ...draftFromBlock(damaged), heatingReplacementAccepted: true };
    expect(validateDraft(draft)).toBeNull();
  });

  it('saves without acknowledgement once real steps are entered', () => {
    // Entering steps IS the decision — there is nothing left to warn about.
    const draft = { ...draftFromBlock(damaged), heating: [{ method: 'Oven', body: '20 min' }] };
    expect(validateDraft(draft)).toBeNull();
  });

  it('does not warn about a block whose heating is simply empty', () => {
    const draft = draftFromBlock({ ...block, heating: [] } as ProductContentDto);
    expect(draft.heatingUnreadable).toBe(false);
    expect(validateDraft(draft)).toBeNull();
  });
});

describe('withheld heating is not damage', () => {
  it('does not warn about a VARIANT whose heating is null', () => {
    // A variant's column is nullable, so null is an ordinary withheld panel that round-trips
    // unchanged — warning about it blocked every unrelated edit behind a false claim of damage.
    const draft = draftFromVariant({
      id: 'v1',
      productId: 'p1',
      selectionJson: '{}',
      servingLabel: 'Per 1',
      nutrition: block.nutrition,
      ingredients: null,
      allergens: null,
      heating: null,
      isActive: true,
    } as ProductContentVariantDto);
    expect(draft.heatingUnreadable).toBe(false);
    expect(validateDraft(draft)).toBeNull();
    expect(wireFromDraft(draft).heatingJson).toBeNull();
  });

  it('still warns about a BLOCK whose heating is null, where the column is required', () => {
    expect(draftFromBlock({ ...block, heating: null } as ProductContentDto).heatingUnreadable).toBe(
      true,
    );
  });
});

describe('unreadable heating — what counts as deciding', () => {
  const damaged = { ...block, heating: null } as ProductContentDto;

  it('a BLANK added row does not satisfy the acknowledgement', () => {
    // The row makes the array non-empty, but `heatingToWire` filters it out and sends null,
    // which the block upsert converts to "[]" — publishing the exact panel this guard exists
    // to make deliberate.
    const draft = { ...draftFromBlock(damaged), heating: [{ method: '', body: '' }] };
    expect(validateDraft(draft)).toMatch(/cannot be read/);
    expect(wireFromDraft(draft).heatingJson).toBeNull();
  });

  it('a HALF-filled row does not satisfy it either', () => {
    const draft = { ...draftFromBlock(damaged), heating: [{ method: 'Oven', body: '' }] };
    expect(validateDraft(draft)).toMatch(/cannot be read/);
  });

  it('one completed step does', () => {
    const draft = { ...draftFromBlock(damaged), heating: [{ method: 'Oven', body: '20 min' }] };
    expect(validateDraft(draft)).toBeNull();
  });
});
