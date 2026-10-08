import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import type { ProductContentDto, ResolvedContentDto } from '@/types/commerce';

import { ContentWorkbench } from './ContentWorkbench';

const block: ProductContentDto = {
  productId: 'p1',
  servingLabel: 'Per serving',
  nutrition: {
    kcal: null,
    proteinGrams: null,
    carbsGrams: null,
    fatGrams: null,
    fibreGrams: null,
    sugarsGrams: null,
    saltGrams: null,
  },
  ingredients: null,
  allergens: 'Legacy declaration',
  allergensPresent: null,
  precautionaryStatement: 'May contain sesame',
  heating: [],
  describesSelectionJson: '{}',
  requiresReview: false,
  contentVersion: 1,
  blockSignature: 'signature',
};

describe('customer allergen preview', () => {
  it('withholds legacy-only allergens and their precautionary statement', () => {
    const html = renderToStaticMarkup(<ContentWorkbench block={block} state="withheld" />);
    expect(html).not.toContain('Legacy declaration');
    expect(html).not.toContain('May contain sesame');
    expect(html).not.toContain('None of the 14');
  });

  it('shows an explicitly reviewed empty list separately from cross-contact precautions', () => {
    const html = renderToStaticMarkup(<ContentWorkbench block={{ ...block, allergensPresent: [] }} state="authored" />);
    expect(html).toContain('None of the 14 regulated allergens declared');
    expect(html).toContain('May contain sesame');
    expect(html).not.toContain('Legacy declaration');
  });

  it('withholds the reviewed list and precautionary statement while stale', () => {
    const html = renderToStaticMarkup(<ContentWorkbench block={{ ...block, allergensPresent: ['Milk'] }} state="review" />);
    expect(html).toContain('Withheld while under review');
    expect(html).not.toContain('Milk');
    expect(html).not.toContain('May contain sesame');
  });

  it('uses the resolved variant declaration instead of the default block', () => {
    const resolved: ResolvedContentDto = {
      servingLabel: block.servingLabel,
      nutrition: block.nutrition,
      ingredients: null,
      allergens: 'Fish',
      allergensPresent: ['Fish'],
      precautionaryStatement: 'May contain mustard',
      heating: [],
      declarationsWithheld: true,
      heatingWithheld: false,
      isStandardPreparation: false,
      isStale: false,
      canonicalSelectionJson: '{"protein":"fish"}',
      matchedVariantSelectionJson: '{"protein":"fish"}',
      contentVersion: 1,
    };
    const html = renderToStaticMarkup(<ContentWorkbench block={{ ...block, allergensPresent: ['Milk'] }} resolved={resolved} state="authored" />);
    expect(html).toContain('Fish');
    expect(html).toContain('May contain mustard');
    expect(html).not.toContain('Milk');
    expect(html).not.toContain('May contain sesame');
  });
});
