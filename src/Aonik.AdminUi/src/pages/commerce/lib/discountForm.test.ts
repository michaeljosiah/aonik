import { describe, expect, it } from 'vitest';
import type { DiscountDto } from '@/types/commerce';
import { discountCreateFrom, discountFormFrom, discountUpdateFrom } from './discountForm';

const existing: DiscountDto = {
  id: 'discount', code: 'WEEKEND', kind: 'FixedAmount', value: 5.25, currency: 'GBP',
  isActive: true, maxRedemptions: 20, timesRedeemed: 4, reservedCount: 2,
  expiresAt: '2027-10-09T12:35:42.1234567Z', eligibleProductIds: ['retired-product', 'product-two'],
  version: 'AAAAAAAAB4s=',
};

describe('discount authoring', () => {
  it('round trips full editable fields, retained product IDs and native version without reauthoring code', () => {
    const form = discountFormFrom(existing);
    form.code = 'DO-NOT-RENAME';
    expect(discountUpdateFrom(form, existing)).toEqual({
      kind: 'FixedAmount', value: 5.25, currency: 'GBP', isActive: true,
      maxRedemptions: 20, expiresAt: existing.expiresAt,
      eligibleProductIds: ['retired-product', 'product-two'], expectedVersion: existing.version,
    });
  });

  it('uses explicit clears and preserves inactive instead of omitting false', () => {
    const form = { ...discountFormFrom(existing), maxRedemptions: '', expiresAtUtc: '', selectedProductsOnly: false, isActive: false };
    expect(discountUpdateFrom(form, existing)).toMatchObject({ maxRedemptions: null, expiresAt: null, eligibleProductIds: null, isActive: false });
  });

  it('creates an active-by-server-default code, with normalized identity and no version', () => {
    const form = { ...discountFormFrom(), code: '  welcome10  ', value: '10.1234' };
    expect(discountCreateFrom(form)).toEqual({
      code: 'WELCOME10', kind: 'Percentage', value: 10.1234, currency: null,
      maxRedemptions: null, expiresAt: null, eligibleProductIds: null,
    });
  });

  it('clears currency for a percentage and normalizes an explicit fixed currency', () => {
    expect(discountUpdateFrom({ ...discountFormFrom(existing), kind: 'Percentage' }, existing).currency).toBeNull();
    expect(discountUpdateFrom({ ...discountFormFrom(existing), currency: ' gbp ' }, existing).currency).toBe('GBP');
  });

  it.each(['', '0', '-1', '1e2', '1.001', 'Infinity', '9999999999999999'])('rejects invalid fixed amount %s without changing the draft', (value) => {
    const form = { ...discountFormFrom(existing), value };
    const before = structuredClone(form);
    expect(() => discountUpdateFrom(form, existing)).toThrow();
    expect(form).toEqual(before);
  });

  it.each(['100.0001', '0.00001'])('rejects percentage bounds/precision %s', (value) => {
    expect(() => discountUpdateFrom({ ...discountFormFrom(existing), kind: 'Percentage', value }, existing)).toThrow();
  });

  it.each(['0', '1.2', '1e2', '2147483648', '5'])('rejects invalid or already occupied cap %s', (maxRedemptions) => {
    expect(() => discountUpdateFrom({ ...discountFormFrom(existing), maxRedemptions }, existing)).toThrow();
  });

  it('allows the exact completed-plus-reserved limit', () => {
    expect(discountUpdateFrom({ ...discountFormFrom(existing), maxRedemptions: '6' }, existing).maxRedemptions).toBe(6);
  });

  it('permits a zero limit for an unused campaign, matching the server', () => {
    const unused = { ...existing, maxRedemptions: 0, timesRedeemed: 0, reservedCount: 0 };
    expect(discountUpdateFrom(discountFormFrom(unused), unused).maxRedemptions).toBe(0);
  });

  it('does not broaden an empty selected-product campaign', () => {
    expect(() => discountUpdateFrom({ ...discountFormFrom(existing), productIds: [] }, existing)).toThrow('Select between');
  });

  it('bounds the unique product selection and does not mutate stored IDs', () => {
    const form = { ...discountFormFrom(existing), productIds: Array.from({ length: 201 }, (_, i) => `product-${i}`) };
    expect(() => discountUpdateFrom(form, existing)).toThrow('Select between');
    form.productIds = ['retired-product', 'retired-product'];
    expect(discountUpdateFrom(form, existing).eligibleProductIds).toEqual(['retired-product']);
    expect(existing.eligibleProductIds).toEqual(['retired-product', 'product-two']);
  });

  it('sends UTC independently of local timezone and accepts explicit seconds', () => {
    const form = { ...discountFormFrom(existing), expiresAtUtc: '2027-07-03T13:45' };
    expect(discountUpdateFrom(form, existing).expiresAt).toBe('2027-07-03T13:45:00.000Z');
    form.expiresAtUtc = '2027-07-03T13:45:22.5';
    expect(discountUpdateFrom(form, existing).expiresAt).toBe('2027-07-03T13:45:22.500Z');
  });

  it.each(['not-a-date', '0000-07-03T13:45', '2027-02-30T13:45', '2027-07-03T24:00', '2027-07-03T13:45Z'])('rejects invalid UTC input %s', (expiresAtUtc) => {
    expect(() => discountUpdateFrom({ ...discountFormFrom(existing), expiresAtUtc }, existing)).toThrow('valid expiry');
  });

  it('keeps an unchanged UTC timestamp with no suffix and does not decode an empty test version', () => {
    const dto = { ...existing, expiresAt: '2027-10-09T12:35:42', version: '' };
    expect(discountUpdateFrom(discountFormFrom(dto), dto)).toMatchObject({ expiresAt: dto.expiresAt, expectedVersion: '' });
  });

  it.each(['', 'A'.repeat(65), 'BAD\nCODE'])('rejects invalid codes', (code) => {
    expect(() => discountCreateFrom({ ...discountFormFrom(), code, value: '10' })).toThrow('discount code');
  });
});
