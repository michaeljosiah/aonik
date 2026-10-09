import { describe, expect, it } from 'vitest';
import { facetSource, optionDraft, rangeLabel, serializeFacetOptions, type FacetOptionDraft } from './facetOptions';

const band = (value: string, min = '', max = ''): FacetOptionDraft => ({ value, label: value, min, max });

describe('facet options', () => {
  it('serializes adjacent half-open bands with open ends and preserves zero', () => {
    const json = serializeFacetOptions([band('negative', '', '0'), band('small', '0', '500'), band('large', '500', '')], 'Range');
    expect(JSON.parse(json)).toEqual([
      { value: 'negative', label: 'negative', min: null, max: 0 },
      { value: 'small', label: 'small', min: 0, max: 500 },
      { value: 'large', label: 'large', min: 500, max: null },
    ]);
    expect(optionDraft({ value: 'zero', label: 'Zero', min: 0, max: null })).toEqual({ value: 'zero', label: 'Zero', min: '0', max: '' });
    expect(rangeLabel(0, 500)).toBe('[0, 500)');
    expect(rangeLabel(null, 0)).toBe('[−∞, 0)');
    expect(rangeLabel(500, null)).toBe('[500, ∞)');
  });

  it.each([
    [band('open')], [band('equal', '5', '5')], [band('reverse', '6', '5')],
    [band('first', '0', '10'), band('overlap', '9', '20')],
    [band('high', '10', '20'), band('low', '0', '5')],
    [band('unbounded', '0', ''), band('after', '50', '100')],
    [band('nan', 'NaN', '10')], [band('infinite', '0', 'Infinity')], [band('overflow', '9007199254740992', '')],
  ])('rejects invalid or overlapping ranges %j', (...rows) => {
    expect(() => serializeFacetOptions(rows, 'Range')).toThrow();
  });

  it('keeps stable trimmed values and labels without carrying range bounds into other match kinds', () => {
    expect(JSON.parse(serializeFacetOptions([{ value: ' veggie ', label: ' Plant based ', min: '0', max: '5' }], 'Tag')))
      .toEqual([{ value: 'veggie', label: 'Plant based' }]);
    expect(() => serializeFacetOptions([band('same'), band(' same ')], 'Tag')).toThrow('unique');
    expect(() => serializeFacetOptions([{ ...band('label'), label: ' ' }], 'Tag')).toThrow('label');
    expect(() => serializeFacetOptions([], 'Category')).toThrow('at least one');
    expect(() => serializeFacetOptions([{ ...band('large'), label: 'x'.repeat(4096) }], 'Attribute')).toThrow('too long');
  });

  it('requires typed nutrition and low-sugar paths to use their supported match kinds', () => {
    expect(facetSource('Range', ' nutrition.kcal ')).toBe('nutrition.kcal');
    expect(facetSource('Attribute', ' lowSugar ')).toBe('lowSugar');
    expect(facetSource('Attribute', 'protein')).toBe('protein');
    expect(facetSource('Tag', 'ignored-draft-source')).toBeNull();
    expect(() => facetSource('Attribute', 'nutrition.proteinGrams')).toThrow('Range');
    expect(() => facetSource('Range', 'lowSugar')).toThrow('Attribute');
    expect(() => facetSource('Range', '')).toThrow();
    expect(() => facetSource('Attribute', 'x'.repeat(129))).toThrow();
  });
});
