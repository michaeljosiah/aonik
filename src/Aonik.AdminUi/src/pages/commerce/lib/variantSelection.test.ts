import { describe, expect, it } from 'vitest';

import {
  describeDrift,
  detectSelectionDrift,
  hasDrift,
  isEmptySelection,
  isMulti,
  parseSelection,
  pickedGroupCount,
  serialiseSelection,
  toggleMulti,
} from './variantSelection';

const single = { key: 'spice', selectionMode: 'One' };
const multi = { key: 'sides', selectionMode: 'Multi' };

describe('parseSelection', () => {
  it('KEEPS an array as an array', () => {
    // Truncating to value[0] made every multi-select combination unauthorable and every
    // existing multi-select variant uneditable.
    expect(parseSelection('{"sides":["rice","plantain"]}')).toEqual({
      sides: ['rice', 'plantain'],
    });
  });

  it('keeps a string as a string', () => {
    expect(parseSelection('{"spice":"hot"}')).toEqual({ spice: 'hot' });
  });

  it('handles mixed shapes in one selection', () => {
    expect(parseSelection('{"spice":"hot","sides":["rice"]}')).toEqual({
      spice: 'hot',
      sides: ['rice'],
    });
  });

  it('survives malformed or absent JSON', () => {
    expect(parseSelection(null)).toEqual({});
    expect(parseSelection('not json')).toEqual({});
    expect(parseSelection('[1,2]')).toEqual({});
  });

  it('drops non-string array members rather than passing them on', () => {
    expect(parseSelection('{"sides":["rice",7,null]}')).toEqual({ sides: ['rice'] });
  });
});

describe('serialiseSelection', () => {
  it('sends a MULTI group as an array even with one choice picked', () => {
    // The MODE decides the shape, not the cardinality — a bare string here is a V5 rejection.
    const json = serialiseSelection({ sides: ['rice'] }, [multi]);
    expect(JSON.parse(json)).toEqual({ sides: ['rice'] });
  });

  it('promotes a stray string to an array for a multi group', () => {
    const json = serialiseSelection({ sides: 'rice' }, [multi]);
    expect(JSON.parse(json)).toEqual({ sides: ['rice'] });
  });

  it('sends a SINGLE group as a bare string', () => {
    const json = serialiseSelection({ spice: 'hot' }, [single]);
    expect(JSON.parse(json)).toEqual({ spice: 'hot' });
  });

  it('demotes a stray array to a string for a single group', () => {
    const json = serialiseSelection({ spice: ['hot'] }, [single]);
    expect(JSON.parse(json)).toEqual({ spice: 'hot' });
  });

  it('omits groups with nothing picked, in either shape', () => {
    const json = serialiseSelection({ spice: '', sides: [] }, [single, multi]);
    expect(JSON.parse(json)).toEqual({});
  });

  it('ignores selections for groups the product does not offer', () => {
    const json = serialiseSelection({ spice: 'hot', gone: 'x' }, [single]);
    expect(JSON.parse(json)).toEqual({ spice: 'hot' });
  });

  it('round-trips a multi-select selection unchanged', () => {
    const original = '{"sides":["rice","plantain"]}';
    expect(JSON.parse(serialiseSelection(parseSelection(original), [multi]))).toEqual(
      JSON.parse(original),
    );
  });
});

describe('toggleMulti', () => {
  it('adds and removes', () => {
    expect(toggleMulti(['rice'], 'plantain')).toEqual(['rice', 'plantain']);
    expect(toggleMulti(['rice', 'plantain'], 'rice')).toEqual(['plantain']);
  });

  it('starts from nothing, and from a stray string', () => {
    expect(toggleMulti(undefined, 'rice')).toEqual(['rice']);
    expect(toggleMulti('rice', 'plantain')).toEqual(['rice', 'plantain']);
  });
});

describe('emptiness helpers', () => {
  it('treats both empty shapes as empty', () => {
    expect(isEmptySelection('')).toBe(true);
    expect(isEmptySelection([])).toBe(true);
    expect(isEmptySelection(undefined)).toBe(true);
    expect(isEmptySelection('hot')).toBe(false);
    expect(isEmptySelection(['rice'])).toBe(false);
  });

  it('counts only groups with something picked', () => {
    expect(pickedGroupCount({ spice: 'hot', sides: [], other: '' })).toBe(1);
  });

  it('identifies multi groups by mode', () => {
    expect(isMulti(multi)).toBe(true);
    expect(isMulti(single)).toBe(false);
  });
});

describe('detectSelectionDrift', () => {
  const offer = [
    { key: 'spice', selectionMode: 'One', choices: [{ key: 'hot' }, { key: 'mild' }] },
    { key: 'sides', selectionMode: 'Multi', choices: [{ key: 'rice' }, { key: 'plantain' }] },
  ];

  it('detects a group the product no longer offers', () => {
    const drift = detectSelectionDrift({ gone: 'x', spice: 'hot' }, offer);
    expect(drift.missingGroups).toEqual(['gone']);
    expect(hasDrift(drift)).toBe(true);
  });

  it('detects a WITHDRAWN choice inside a group that still exists', () => {
    // The group survives, so a group-level check sees nothing wrong — and the server then
    // rejects every edit with V2/V3.
    const drift = detectSelectionDrift({ spice: 'nuclear' }, offer);
    expect(drift.withdrawnChoices).toEqual(['spice.nuclear']);
  });

  it('detects a MULTI→ONE mode change that would silently truncate', () => {
    // The nastiest of the three: every group and choice is still valid, so nothing looks
    // wrong, and serialisation quietly keeps the first member — moving the variant onto a
    // different combination while carrying content authored for the original.
    const tightened = [
      { key: 'sides', selectionMode: 'One', choices: [{ key: 'rice' }, { key: 'plantain' }] },
    ];
    const drift = detectSelectionDrift({ sides: ['rice', 'plantain'] }, tightened);
    expect(drift.shapeChanged).toEqual(['sides']);
    expect(describeDrift(drift)).toMatch(/now single-select/);
  });

  it('does NOT flag a single-choice multi selection against a tightened group', () => {
    // One stored choice still expresses the same combination after the mode narrows.
    const tightened = [{ key: 'sides', selectionMode: 'One', choices: [{ key: 'rice' }] }];
    expect(hasDrift(detectSelectionDrift({ sides: ['rice'] }, tightened))).toBe(false);
  });

  it('reports a clean selection as undrifted', () => {
    const drift = detectSelectionDrift({ spice: 'hot', sides: ['rice', 'plantain'] }, offer);
    expect(hasDrift(drift)).toBe(false);
    expect(describeDrift(drift)).toBe('');
  });

  it('ignores groups with nothing picked', () => {
    expect(hasDrift(detectSelectionDrift({ gone: '', other: [] }, offer))).toBe(false);
  });
});

describe('detectSelectionDrift — a group ADDED to the offer', () => {
  const offer = [
    { key: 'spice', selectionMode: 'One', choices: [{ key: 'hot' }, { key: 'mild' }] },
    { key: 'sides', selectionMode: 'Multi', choices: [{ key: 'rice' }] },
  ];

  it('flags an effective group a STORED canonical selection does not name', () => {
    // The only drift that is an ABSENCE, so iterating what is stored can never see it. The
    // stored selection was canonical and complete when written; the product has since gained
    // `sides`, and re-serialising lets the server default it in — retargeting the variant onto
    // a preparation that did not exist when its allergens were written.
    const drift = detectSelectionDrift({ spice: 'hot' }, offer, { storedIsCanonical: true });
    expect(drift.addedGroups).toEqual(['sides']);
    expect(hasDrift(drift)).toBe(true);
    expect(describeDrift(drift)).toMatch(/offered since this combination was authored/);
  });

  it('does NOT flag absence while a new combination is being composed', () => {
    // A partial selection is the intended input there — the server fills the rest — so treating
    // absence as drift would refuse every create.
    expect(hasDrift(detectSelectionDrift({ spice: 'hot' }, offer))).toBe(false);
  });

  it('ignores a group with no offerable choices, which cannot be defaulted in', () => {
    const withEmpty = [...offer, { key: 'extras', selectionMode: 'One', choices: [] }];
    const drift = detectSelectionDrift({ spice: 'hot', sides: ['rice'] }, withEmpty, {
      storedIsCanonical: true,
    });
    expect(drift.addedGroups).toEqual([]);
  });

  it('reports a complete stored selection as undrifted', () => {
    const drift = detectSelectionDrift({ spice: 'hot', sides: ['rice'] }, offer, {
      storedIsCanonical: true,
    });
    expect(hasDrift(drift)).toBe(false);
  });
});
