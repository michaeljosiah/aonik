import { describe, expect, it } from 'vitest';

import { describePreparation } from './preparation';

const groups = [
  {
    key: 'protein',
    label: 'Protein',
    choices: [
      { key: 'salmon', label: 'Salmon fillet' },
      { key: 'prawns', label: 'King prawns' },
    ],
  },
  { key: 'sides', label: 'Sides', choices: [{ key: 'rice', label: 'Jollof rice' }] },
];

describe('describePreparation', () => {
  it('labels each group and choice, so the assertion is checkable', () => {
    // V-C9 proves the binding did not move during the request. It says nothing about whether a
    // person looked at what they were agreeing to — which is what this renders.
    expect(describePreparation('{"protein":"salmon"}', groups)).toEqual([
      { group: 'Protein', choice: 'Salmon fillet' },
    ]);
  });

  it('joins a multi-select group', () => {
    expect(describePreparation('{"sides":["rice"]}', groups)).toEqual([
      { group: 'Sides', choice: 'Jollof rice' },
    ]);
  });

  it('falls back to raw KEYS rather than hiding a group', () => {
    // An unlabelled line is still checkable; a dropped one understates what is being confirmed.
    expect(describePreparation('{"gone":"mystery"}', groups)).toEqual([
      { group: 'gone', choice: 'mystery' },
    ]);
  });

  it('handles a product with no options', () => {
    expect(describePreparation('{}', groups)).toEqual([]);
    expect(describePreparation('', groups)).toEqual([]);
  });
});
