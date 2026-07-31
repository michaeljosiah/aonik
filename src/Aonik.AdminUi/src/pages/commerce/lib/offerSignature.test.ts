import { describe, expect, it } from 'vitest';

import { defaultCombination, defaultSignature } from './offerSignature';

const groups = [
  { key: 'spice', defaultChoiceKey: 'mild', choices: [{ key: 'mild' }, { key: 'hot' }] },
  { key: 'sides', defaultChoiceKey: 'rice', choices: [{ key: 'rice' }] },
];

describe('defaultCombination', () => {
  it('is the preparation served when the customer picks nothing', () => {
    expect(defaultCombination(groups)).toEqual({ spice: 'mild', sides: 'rice' });
  });

  it('ignores a group with no offerable choices', () => {
    // It cannot be defaulted into a selection, so it cannot change which preparation is served.
    const withEmpty = [...groups, { key: 'extras', defaultChoiceKey: 'gold', choices: [] }];
    expect(defaultCombination(withEmpty)).toEqual({ spice: 'mild', sides: 'rice' });
  });

  it('ignores a group with no default', () => {
    const noDefault = [{ key: 'spice', defaultChoiceKey: '', choices: [{ key: 'mild' }] }];
    expect(defaultCombination(noDefault)).toEqual({});
  });
});

describe('defaultSignature', () => {
  it('changes when the default MOVES — the case no content row records', () => {
    // A block's contentVersion versions the row; the preparation it describes is not in that
    // row, so an option write moves what the figures publish against invisibly.
    const moved = [{ ...groups[0], defaultChoiceKey: 'hot' }, groups[1]];
    expect(defaultSignature(moved)).not.toBe(defaultSignature(groups));
  });

  it('changes when a group is ADDED, which brings a new default with it', () => {
    const added = [...groups, { key: 'bread', defaultChoiceKey: 'none', choices: [{ key: 'none' }] }];
    expect(defaultSignature(added)).not.toBe(defaultSignature(groups));
  });

  it('is order-independent, so a reordered offer is not a changed one', () => {
    expect(defaultSignature([groups[1], groups[0]])).toBe(defaultSignature(groups));
  });

  it('is stable across reads of an unchanged offer', () => {
    expect(defaultSignature(groups)).toBe(defaultSignature([...groups]));
  });
});
