import { describe, expect, it } from 'vitest';

import {
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
