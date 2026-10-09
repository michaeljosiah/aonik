import type { ReactNode } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import type { RefundContextDto, RefundDto, RefundPreviewDto } from '@/types/commerceRefunds';
import { OrderRefundDialog, RefundPreviewDetails } from './OrderRefundDialog';
import { OrderRefundHistory } from './OrderRefundHistory';

vi.mock('@/components/ui/dialog', () => {
  const Container = ({ children }: { children: ReactNode }) => <div>{children}</div>;
  return { Dialog: Container, DialogContent: Container, DialogDescription: Container, DialogFooter: Container, DialogHeader: Container, DialogTitle: Container };
});

const refund: RefundDto = { refundId: 'refund-one', orderId: 'order-one', status: 'Pending', requestedAtUtc: '2026-10-09T11:00:00Z',
  requestedBy: 'staff-one', reason: 'Broken seal', total: 12, currency: 'GBP', cashAmount: 7, giftAmount: 5,
  redeemedPointsToRestore: 100, earnedPointsToReverse: 12, effectsAppliedAtUtc: null, failureReason: null, canReconcile: true };
const context: RefundContextDto = { currency: 'GBP', canRequest: false, disabledReason: 'A refund is pending.', remainingTotal: 8,
  status: 'RefundPending', components: [], history: [refund] };
const preview: RefundPreviewDto = { currency: 'GBP', version: 'v', selections: [], total: 12, cashAmount: 7, giftAmount: 5,
  redeemedPointsToRestore: 100, earnedPointsToReverse: 12, warnings: ['Create the linked invoice credit note manually.'] };

describe('operator refund presentation', () => {
  it('displays the authoritative cash/gift/points split and manual credit note warning before confirmation', () => {
    const html = renderToStaticMarkup(<RefundPreviewDetails preview={preview} />);
    expect(html).toContain('Cash refund');
    expect(html).toContain('£7.00');
    expect(html).toContain('Restore to original gift card');
    expect(html).toContain('£5.00');
    expect(html).toContain('100');
    expect(html).toContain('Create the linked invoice credit note manually.');
    expect(html).toContain('Fulfilment does not change.');
  });

  it('keeps pending refunds and financial effects distinct from success', () => {
    const html = renderToStaticMarkup(<OrderRefundHistory context={context} canWrite busy={false} onCheck={() => {}} onReconcile={() => {}} />);
    expect(html).toContain('Pending');
    expect(html).toContain('Broken seal');
    expect(html).toContain('staff-one');
    expect(html).toContain('Reconcile refund');
    expect(html).not.toContain('Succeeded');
    expect(html).not.toContain('Effects recorded');
  });

  it('allows read-only staff to check history without offering money mutations', () => {
    const html = renderToStaticMarkup(<OrderRefundHistory context={context} canWrite={false} busy={false} onCheck={() => {}} onReconcile={() => {}} />);
    expect(html).toContain('Check status');
    expect(html).not.toContain('Reconcile refund');
    expect(html).not.toContain('Confirm refund');
  });

  it('respects the server reconciliation flag and shows recorded failure truthfully', () => {
    const html = renderToStaticMarkup(<OrderRefundHistory context={{ ...context, history: [{ ...refund, status: 'Failed', failureReason: 'Provider rejected the request.', canReconcile: false }] }}
      canWrite busy={false} onCheck={() => {}} onReconcile={() => {}} />);
    expect(html).toContain('Provider rejected the request.');
    expect(html).not.toContain('Reconcile refund');
  });

  it('permits explicit full remaining for a zero-money points component but cannot confirm without a preview', () => {
    const html = renderToStaticMarkup(<OrderRefundDialog context={{ ...context, components: [{ componentId: 'points', kind: 'Goods', label: 'Points-funded item',
      originalAmount: 0, remainingAmount: 0, canRefund: true, unavailableReason: null }] }} reason="Missing item" inputs={{}}
      preview={null} busy={false} error={null} onReason={() => {}} onLine={() => {}} onSelectAll={() => {}} onPreview={() => {}} onConfirm={() => {}} onClose={() => {}} />);
    expect(html).toContain('Full remaining component (including points)');
    expect(html).not.toContain('Partial amount');
    expect(html).not.toContain('Confirm refund request');
    expect(html).toContain('Preview refund');
  });
});
