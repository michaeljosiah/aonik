import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { formatCurrency, formatDateTime } from '@/lib/format';
import type { RefundContextDto } from '@/types/commerceRefunds';
import { refundStatusLabel } from '../lib/refundForm';

export function OrderRefundHistory({ context, canWrite, busy, onReconcile, onCheck }: {
  context: RefundContextDto; canWrite: boolean; busy: boolean;
  onReconcile: (refundId: string) => void; onCheck: (refundId: string) => void;
}) {
  return <AonikCard title="Refund history" padding={12}>
    <div className="flex flex-wrap items-center justify-between gap-2">
      <Pill tone="default">{refundStatusLabel(context.status)}</Pill>
      <span className="text-xs text-muted-foreground">Remaining {formatCurrency(context.remainingTotal, context.currency)}</span>
    </div>
    {context.history.length === 0 ? <p className="mt-3 text-sm text-muted-foreground">No refund requests recorded.</p>
      : <ol className="mt-3 space-y-4">{context.history.map((refund) => <li key={refund.refundId} className="space-y-2 border-t border-border pt-3">
        <div className="flex flex-wrap justify-between gap-2 text-sm">
          <span className="font-medium">{refundStatusLabel(refund.status)}</span>
          <span>{formatCurrency(refund.total, refund.currency)}</span>
        </div>
        <p className="text-xs text-muted-foreground">Requested {formatDateTime(refund.requestedAtUtc)}</p>
        <p className="whitespace-pre-wrap break-words text-sm">{refund.reason}</p>
        <p className="text-xs text-muted-foreground">Cash {formatCurrency(refund.cashAmount, refund.currency)} · Gift card {formatCurrency(refund.giftAmount, refund.currency)} · Points to restore {refund.redeemedPointsToRestore} · Earned points to reverse {refund.earnedPointsToReverse}</p>
        {refund.effectsAppliedAtUtc && <p className="text-xs text-muted-foreground">Effects recorded {formatDateTime(refund.effectsAppliedAtUtc)}</p>}
        {refund.failureReason && <p role="status" className="text-sm text-destructive">{refund.failureReason}</p>}
        <p className="break-all text-xs text-muted-foreground">Reference: {refund.refundId}{refund.requestedBy ? ` · Staff: ${refund.requestedBy}` : ''}</p>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={busy} onClick={() => onCheck(refund.refundId)}>Check status</Button>
          {canWrite && refund.canReconcile && <Button variant="outline" size="sm" disabled={busy} onClick={() => onReconcile(refund.refundId)}>Reconcile refund</Button>}
        </div>
      </li>)}</ol>}
  </AonikCard>;
}
