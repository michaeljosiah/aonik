import type { RefundComponentDto, RefundDraft, RefundPreviewDto, RefundRequest, RefundSelection } from '@/types/commerceRefunds';
import { validateDecimalInput } from './decimalInput';

export interface RefundLineInput { mode: 'none' | 'full' | 'amount'; amount: string }
export type RefundInputs = Record<string, RefundLineInput>;

export function refundDraft(reason: string, inputs: RefundInputs, components: RefundComponentDto[]): RefundDraft {
  const trimmed = reason.trim();
  if (!trimmed || trimmed.length > 500) throw new Error('Enter a reason of 1–500 characters.');
  const selections = components.flatMap<RefundSelection>((component) => {
    const input = inputs[component.componentId];
    if (!input || input.mode === 'none') return [];
    if (!component.canRefund) throw new Error(`${component.label} is no longer available for refund.`);
    if (input.mode === 'full') return [{ componentId: component.componentId, amount: null, fullRemaining: true }];
    const error = validateDecimalInput(input.amount, { scale: 2, max: component.remainingAmount, subject: 'The refund amount' });
    if (error) throw new Error(error);
    if (!input.amount.trim() || Number(input.amount) <= 0) throw new Error('Enter a refund amount greater than zero.');
    return [{ componentId: component.componentId, amount: Number(input.amount), fullRemaining: false }];
  });
  if (selections.length === 0 || selections.length > 500) throw new Error('Select between 1 and 500 refundable components.');
  return { reason: trimmed, selections };
}

export function refundRequest(draft: RefundDraft, preview: RefundPreviewDto, refundId: string): RefundRequest {
  return { refundId, reason: draft.reason, expectedPreviewVersion: preview.version,
    selections: preview.selections.map((selection) => ({ ...selection })) };
}

export function refundStatusLabel(status: string): string {
  return ({ None: 'No refunds', PartiallyRefunded: 'Partially refunded', Refunded: 'Refunded',
    RefundPending: 'Refund pending', NeedsReconciliation: 'Needs reconciliation', Requested: 'Requested',
    Unknown: 'Outcome unknown', Pending: 'Pending', Succeeded: 'Succeeded', Failed: 'Failed' } as Record<string, string>)[status] ?? status;
}

export function refundError(error: unknown, fallback: string): string {
  return error && typeof error === 'object' && 'userMessage' in error && error.userMessage
    ? String(error.userMessage) : error instanceof Error ? error.message : fallback;
}

export function refundStorageKey(tenantId: string, orderId: string): string {
  return `aonik:refund-request:${tenantId}:${orderId}`;
}

export function refundWasRejected(error: unknown): boolean {
  if (!error || typeof error !== 'object' || !('response' in error)) return false;
  const response = error.response;
  return !!response && typeof response === 'object' && 'status' in response
    && [400, 409, 422].includes(Number(response.status));
}

// Retain the exact admitted request across drawer closure/reload. A network error cannot
// establish whether money moved, so retries must not mint a second operation ID.
export function readRefundRequest(value: string | null): RefundRequest | null {
  if (value === null) return null;
  if (value.length > 100_000) throw new Error('The saved refund request cannot be read. Check refund history before continuing.');
  const request = JSON.parse(value) as RefundRequest;
  if (!request || typeof request.refundId !== 'string' || !/^[\da-f]{8}-(?:[\da-f]{4}-){3}[\da-f]{12}$/i.test(request.refundId)
    || typeof request.reason !== 'string' || !request.reason.trim() || request.reason.length > 500
    || typeof request.expectedPreviewVersion !== 'string' || !request.expectedPreviewVersion
    || !Array.isArray(request.selections) || !request.selections.length || request.selections.length > 500
    || request.selections.some((line) => !line || typeof line.componentId !== 'string'
      || !(line.fullRemaining === true && line.amount === null
        || line.fullRemaining === false && typeof line.amount === 'number' && Number.isFinite(line.amount) && line.amount > 0))) {
    throw new Error('The saved refund request cannot be read. Check refund history before continuing.');
  }
  return request;
}
