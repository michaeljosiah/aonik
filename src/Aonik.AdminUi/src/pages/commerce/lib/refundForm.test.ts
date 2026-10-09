import { describe, expect, it } from 'vitest';
import type { RefundComponentDto, RefundPreviewDto } from '@/types/commerceRefunds';
import { readRefundRequest, refundDraft, refundRequest, refundStatusLabel, refundStorageKey, refundWasRejected } from './refundForm';

const components: RefundComponentDto[] = [
  { componentId: 'goods:original-id', kind: 'Goods', label: 'Dinner', originalAmount: 15, remainingAmount: 10, canRefund: true, unavailableReason: null },
  { componentId: 'points:original-id', kind: 'Goods', label: 'Points-funded item', originalAmount: 0, remainingAmount: 0, canRefund: true, unavailableReason: null },
];
const preview: RefundPreviewDto = { currency: 'GBP', version: 'price-state-v1', total: 7.25, cashAmount: 2.25, giftAmount: 5,
  redeemedPointsToRestore: 37, earnedPointsToReverse: 12, warnings: [], selections: [{ componentId: components[0].componentId, amount: 7.25, fullRemaining: false }] };
const id = '8d074a98-7707-41aa-a034-b512f4ef7f1a';

describe('refund form and recovery state', () => {
  it('selects full remaining even when only points can be restored', () => {
    expect(refundDraft('  Missing item  ', { [components[1].componentId]: { mode: 'full', amount: '' } }, components))
      .toEqual({ reason: 'Missing item', selections: [{ componentId: components[1].componentId, amount: null, fullRemaining: true }] });
  });

  it('sends only explicitly selected original components and preserves a partial amount', () => {
    expect(refundDraft('Damaged', { [components[0].componentId]: { mode: 'amount', amount: '7.25' },
      'injected-id': { mode: 'full', amount: '' } }, components).selections)
      .toEqual([{ componentId: components[0].componentId, amount: 7.25, fullRemaining: false }]);
  });

  it.each(['', '0', '-1', '1e-2', '0x10', 'NaN', 'Infinity', '1.001', '10.01'])('rejects invalid partial money %s', (amount) => {
    expect(() => refundDraft('Damaged', { [components[0].componentId]: { mode: 'amount', amount } }, components)).toThrow();
  });

  it.each(['', ' '.repeat(10), 'a'.repeat(501)])('requires a bounded reason', (reason) => {
    expect(() => refundDraft(reason, { [components[0].componentId]: { mode: 'full', amount: '' } }, components)).toThrow('reason');
  });

  it('rejects no selections and a component no longer refundable', () => {
    expect(() => refundDraft('Damaged', {}, components)).toThrow('Select');
    expect(() => refundDraft('Damaged', { [components[0].componentId]: { mode: 'full', amount: '' } },
      [{ ...components[0], canRefund: false }])).toThrow('no longer');
  });

  it('binds the exact server preview and survives reload without creating a new ID or recomputing allocations', () => {
    const draft = refundDraft('Damaged', { [components[0].componentId]: { mode: 'amount', amount: '7.25' } }, components);
    const request = refundRequest(draft, preview, id);
    const restored = readRefundRequest(JSON.stringify(request));
    expect(restored).toEqual({ refundId: id, reason: 'Damaged', expectedPreviewVersion: preview.version, selections: preview.selections });
    draft.selections[0].amount = 1;
    expect(request.selections[0].amount).toBe(7.25);
    expect(request.selections).not.toBe(preview.selections);
  });

  it('separates saved requests by identity, tenant and order', () => {
    expect(refundStorageKey('identity-a:tenant-a', 'order-a')).not.toBe(refundStorageKey('identity-b:tenant-a', 'order-a'));
    expect(refundStorageKey('identity-a:tenant-a', 'order-a')).not.toBe(refundStorageKey('identity-a:tenant-b', 'order-a'));
    expect(refundStorageKey('identity-a:tenant-a', 'order-a')).not.toBe(refundStorageKey('identity-a:tenant-a', 'order-b'));
  });

  it('fails closed for corrupt saved requests instead of creating a replacement ID', () => {
    expect(readRefundRequest(null)).toBeNull();
    for (const value of ['not-json', '{}', JSON.stringify({ refundId: id, reason: 'Damaged', selections: [], expectedPreviewVersion: 'v' })]) {
      expect(() => readRefundRequest(value)).toThrow();
    }
  });

  it.each([400, 409, 422])('requires a new preview after definitive rejection %s', (status) => {
    expect(refundWasRejected({ response: { status }, userMessage: 'Rejected' })).toBe(true);
  });

  it.each([undefined, 401, 403, 404, 429, 500, 502, 503])('preserves the operation after uncertain or unauthorised response %s', (status) => {
    expect(refundWasRejected({ response: { status } })).toBe(false);
  });

  it('keeps unknown/pending statuses distinct from succeeded, including future server statuses', () => {
    expect(refundStatusLabel('Unknown')).toBe('Outcome unknown');
    expect(refundStatusLabel('Pending')).toBe('Pending');
    expect(refundStatusLabel('Requested')).toBe('Requested');
    expect(refundStatusLabel('Succeeded')).toBe('Succeeded');
    expect(refundStatusLabel('ProviderReview')).toBe('ProviderReview');
  });
});
