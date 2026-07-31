import { describe, expect, it } from 'vitest';

import type { AdminProductDetailDto } from '@/types/commerce';

import {
  buildMediaReplacement,
  buildProductPatch,
  formFromProduct,
  isEmptyPatch,
  moveItem,
  validateAttributesJson,
  type ProductEditorForm,
} from './productForm';

function baseForm(overrides: Partial<ProductEditorForm> = {}): ProductEditorForm {
  return {
    name: 'Jollof Rice',
    description: 'Smoky party rice',
    status: 'Active',
    categoryId: 'cat-1',
    tags: ['vegan'],
    attributesJson: '{"spice":"medium"}',
    searchKeywords: ['party', 'naija'],
    ...overrides,
  };
}

describe('buildProductPatch', () => {
  it('sends nothing when nothing changed', () => {
    const patch = buildProductPatch(baseForm(), baseForm());
    expect(patch).toEqual({});
    expect(isEmptyPatch(patch)).toBe(true);
  });

  it('sends ONLY the touched member — editing the name must not resend keywords', () => {
    // The acceptance criterion: untouched keywords survive the edit.
    const patch = buildProductPatch(baseForm(), baseForm({ name: 'Jollof Rice XL' }));
    expect(patch).toEqual({ name: 'Jollof Rice XL' });
    expect(patch.searchKeywordsJson).toBeUndefined();
    expect(patch.tagsJson).toBeUndefined();
  });

  it('writes keywords as searchKeywordsJson — the array member is not on the write contract', () => {
    const patch = buildProductPatch(baseForm(), baseForm({ searchKeywords: ['party', 'rice'] }));
    expect(patch.searchKeywordsJson).toBe(JSON.stringify(['party', 'rice']));
    expect((patch as Record<string, unknown>).searchKeywords).toBeUndefined();
  });

  it('treats keyword REORDERING as a change — the operator authored that order', () => {
    const patch = buildProductPatch(baseForm(), baseForm({ searchKeywords: ['naija', 'party'] }));
    expect(patch.searchKeywordsJson).toBe(JSON.stringify(['naija', 'party']));
  });

  it('sends an emptied keyword list rather than dropping the edit', () => {
    const patch = buildProductPatch(baseForm(), baseForm({ searchKeywords: [] }));
    expect(patch.searchKeywordsJson).toBe('[]');
  });

  it('clears a category with the explicit flag, not an absent member', () => {
    // An absent member means "untouched", so null needs its own signal.
    const patch = buildProductPatch(baseForm(), baseForm({ categoryId: null }));
    expect(patch).toEqual({ clearCategory: true });
    expect(patch.categoryId).toBeUndefined();
  });

  it('sends a changed category by id', () => {
    const patch = buildProductPatch(baseForm(), baseForm({ categoryId: 'cat-2' }));
    expect(patch).toEqual({ categoryId: 'cat-2' });
  });

  it('serializes tags as JSON when touched', () => {
    const patch = buildProductPatch(baseForm(), baseForm({ tags: ['vegan', 'gluten-free'] }));
    expect(patch.tagsJson).toBe(JSON.stringify(['vegan', 'gluten-free']));
  });

  it('carries several touched members at once', () => {
    const patch = buildProductPatch(
      baseForm(),
      baseForm({ name: 'New', status: 'Draft', attributesJson: '{"spice":"hot"}' }),
    );
    expect(patch).toEqual({ name: 'New', status: 'Draft', attributesJson: '{"spice":"hot"}' });
  });
});

describe('formFromProduct', () => {
  it('parses tags and keeps keywords as the array the admin read exposes', () => {
    const product = {
      id: 'p1',
      slug: 'jollof',
      name: 'Jollof',
      description: '',
      status: 'Active',
      kind: 'Simple',
      categoryId: null,
      tagsJson: '["vegan"]',
      attributesJson: '{"spice":"medium"}',
      searchKeywords: ['party'],
    } as unknown as AdminProductDetailDto;

    expect(formFromProduct(product)).toMatchObject({
      tags: ['vegan'],
      searchKeywords: ['party'],
      categoryId: null,
    });
  });

  it('survives malformed legacy JSON rather than breaking the editor', () => {
    const product = {
      name: 'X',
      description: '',
      status: 'Active',
      tagsJson: 'not json',
      attributesJson: '{}',
      searchKeywords: [],
    } as unknown as AdminProductDetailDto;
    expect(formFromProduct(product).tags).toEqual([]);
  });
});

describe('buildMediaReplacement', () => {
  it('carries NO sort field — position is the order the server assigns', () => {
    const lines = buildMediaReplacement([
      { url: 'https://cdn/a.jpg' },
      { url: 'https://cdn/b.jpg', kind: 'image' },
    ]);
    expect(lines).toEqual([
      { url: 'https://cdn/a.jpg', kind: null },
      { url: 'https://cdn/b.jpg', kind: 'image' },
    ]);
    for (const line of lines) {
      expect(line).not.toHaveProperty('sortOrder');
    }
  });

  it('drops blank rows so an empty add-field cannot persist an empty image', () => {
    expect(buildMediaReplacement([{ url: '  ' }, { url: 'https://cdn/a.jpg' }])).toHaveLength(1);
  });
});

describe('moveItem', () => {
  it('reorders, which is what actually changes the saved order', () => {
    expect(moveItem(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b']);
    expect(moveItem(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
  });

  it('is a no-op for out-of-range or identical positions', () => {
    expect(moveItem(['a', 'b'], 0, 0)).toEqual(['a', 'b']);
    expect(moveItem(['a', 'b'], -1, 1)).toEqual(['a', 'b']);
    expect(moveItem(['a', 'b'], 0, 9)).toEqual(['a', 'b']);
  });
});

describe('validateAttributesJson', () => {
  it('accepts a JSON object and the empty value', () => {
    expect(validateAttributesJson('{"spice":"hot"}')).toBeNull();
    expect(validateAttributesJson('  ')).toBeNull();
  });

  it('rejects malformed JSON', () => {
    expect(validateAttributesJson('{oops')).toMatch(/valid JSON/);
  });

  it('rejects a non-object, since facet paths traverse from the root', () => {
    expect(validateAttributesJson('[1,2]')).toMatch(/object/);
    expect(validateAttributesJson('"text"')).toMatch(/object/);
    expect(validateAttributesJson('null')).toMatch(/object/);
  });
});
