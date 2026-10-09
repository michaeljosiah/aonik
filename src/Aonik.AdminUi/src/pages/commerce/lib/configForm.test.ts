import { describe, expect, it } from 'vitest';
import type { StorefrontConfigDto } from '@/types/commerce';
import { buildConfigCommand, CLEARABLE_CONFIG_FIELDS, configDraftFrom, validateConfigDraft } from './configForm';

const config: StorefrontConfigDto = {
  currency: 'GBP', recommendedChoiceLabel: 'Chef recommends', resultsPageSize: 8,
  backToTopTrigger: { type: 'cardIndex', value: 10, extension: { enabled: true } },
  delivery: { listAmount: 7.95, chargedAmount: 5.95 },
  defaultBoxSlug: 'retired-box', extrasCollectionSlug: 'inactive-extras', box: null,
  greetingCard: { amount: 3, currency: 'GBP' }, saleTerms: { version: '2026', url: 'https://example.test/terms' }, signatureTag: 'signature',
};

describe('storefront config partial update', () => {
  it('sends only the touched label without rewriting prices, choices or newer configuration fields', () => {
    const values = { ...configDraftFrom(config), recommendedChoiceLabel: 'Our choice' };
    expect(JSON.stringify(buildConfigCommand({ recommendedChoiceLabel: true }, values))).toBe('{"recommendedChoiceLabel":"Our choice"}');
    expect(buildConfigCommand({}, values)).toEqual({});
  });

  it.each(CLEARABLE_CONFIG_FIELDS)('clears only the supported %s override using an explicit empty string', (field) => {
    const values = { ...configDraftFrom(config), [field]: '' };
    expect(buildConfigCommand({ [field]: true }, values)).toEqual({ [field]: '' });
  });

  it.each(['deliveryListAmount', 'deliveryChargedAmount', 'resultsPageSize', 'extrasCollectionSlug'] as const)(
    'rejects a blank %s instead of inventing zero or a clear operation', (field) => {
      expect(() => buildConfigCommand({ [field]: true }, { ...configDraftFrom(config), [field]: '' })).toThrow();
    });

  it('sends an explicitly authored free charge as numeric zero and preserves the list amount by omission', () => {
    expect(buildConfigCommand({ deliveryChargedAmount: true }, { ...configDraftFrom(config), deliveryChargedAmount: '0' }))
      .toEqual({ deliveryChargedAmount: 0 });
  });

  it.each(['-1', '1e2', 'Infinity', '0x10', '0.001', '1000000', '1.2.3'])(
    'rejects unsupported delivery amount %s without mutating the draft', (value) => {
      const values = { ...configDraftFrom(config), deliveryChargedAmount: value };
      const before = { ...values };
      expect(() => buildConfigCommand({ deliveryChargedAmount: true }, values)).toThrow();
      expect(values).toEqual(before);
    });

  it.each(['0', '201', '1.5', '1e2', '-1'])('rejects invalid page size %s', (value) => {
    expect(() => buildConfigCommand({ resultsPageSize: true }, { ...configDraftFrom(config), resultsPageSize: value })).toThrow();
  });

  it('accepts the exact supported numeric boundaries', () => {
    expect(buildConfigCommand({ resultsPageSize: true, deliveryListAmount: true },
      { ...configDraftFrom(config), resultsPageSize: '200', deliveryListAmount: '999999.99' }))
      .toEqual({ resultsPageSize: 200, deliveryListAmount: 999999.99 });
  });

  it.each(['null', '[]', 'true', '42', '"string"', '{broken}'])(
    'rejects non-object or malformed trigger %s', (value) => {
      expect(validateConfigDraft({ backToTopTriggerJson: true }, { ...configDraftFrom(config), backToTopTriggerJson: value })).not.toBeNull();
    });

  it('preserves arbitrary object members and retained unavailable selections without reauthoring them', () => {
    const values = configDraftFrom(config);
    expect(values.defaultBoxSlug).toBe('retired-box');
    expect(values.extrasCollectionSlug).toBe('inactive-extras');
    const command = buildConfigCommand({ backToTopTriggerJson: true }, values);
    expect(JSON.parse(command.backToTopTriggerJson!)).toEqual(config.backToTopTrigger);
    expect(command.defaultBoxSlug).toBeUndefined();
    expect(command.extrasCollectionSlug).toBeUndefined();
  });

  it('validates only touched values so legacy settings do not prevent an unrelated correction', () => {
    const values = { ...configDraftFrom(config), deliveryChargedAmount: '0.001', extrasCollectionSlug: '' };
    expect(buildConfigCommand({ recommendedChoiceLabel: true }, values)).toEqual({ recommendedChoiceLabel: config.recommendedChoiceLabel });
  });

  it('enforces settings string bounds and distinct slug bounds before creating a payload', () => {
    const values = configDraftFrom(config);
    expect(() => buildConfigCommand({ recommendedChoiceLabel: true }, { ...values, recommendedChoiceLabel: 'a'.repeat(4001) })).toThrow();
    expect(() => buildConfigCommand({ backToTopTriggerJson: true }, { ...values, backToTopTriggerJson: JSON.stringify({ text: 'a'.repeat(4000) }) })).toThrow();
    expect(() => buildConfigCommand({ extrasCollectionSlug: true }, { ...values, extrasCollectionSlug: 'a'.repeat(65) })).toThrow();
    expect(buildConfigCommand({ defaultBoxSlug: true }, { ...values, defaultBoxSlug: 'a'.repeat(160) })).toEqual({ defaultBoxSlug: 'a'.repeat(160) });
  });
});
