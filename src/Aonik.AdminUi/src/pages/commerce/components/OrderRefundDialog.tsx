import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Textarea } from '@/components/ui/textarea';
import { formatCurrency } from '@/lib/format';
import type { RefundContextDto, RefundPreviewDto } from '@/types/commerceRefunds';
import type { RefundInputs, RefundLineInput } from '../lib/refundForm';

interface Props {
  context: RefundContextDto;
  reason: string;
  inputs: RefundInputs;
  preview: RefundPreviewDto | null;
  busy: boolean;
  error: string | null;
  onReason: (reason: string) => void;
  onLine: (componentId: string, input: RefundLineInput) => void;
  onSelectAll: () => void;
  onPreview: () => void;
  onConfirm: () => void;
  onClose: () => void;
}

export function OrderRefundDialog(props: Props) {
  const { context, reason, inputs, preview, busy, error, onReason, onLine, onSelectAll, onPreview, onConfirm, onClose } = props;
  return <Dialog open onOpenChange={(open) => { if (!open && !busy) onClose(); }}>
    <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl" showCloseButton={!busy}>
      <DialogHeader>
        <DialogTitle>Refund order</DialogTitle>
        <DialogDescription>Select original charges and review the server-calculated refund before confirming.</DialogDescription>
      </DialogHeader>
      <fieldset disabled={busy} className="min-w-0 space-y-4">
        <div className="space-y-2">
          <Label htmlFor="refund-reason">Reason</Label>
          <Textarea id="refund-reason" value={reason} onChange={(event) => onReason(event.target.value)} maxLength={500} rows={3} />
        </div>
        <Button variant="outline" size="sm" onClick={onSelectAll}>Select full remaining refund</Button>
        {context.components.map((component, index) => {
          const input = inputs[component.componentId] ?? { mode: 'none', amount: '' };
          return <div key={component.componentId} className="space-y-2 rounded-md border border-border p-3">
            <Label htmlFor={`refund-component-${index}`}>{component.label}</Label>
            <p className="text-xs text-muted-foreground">{component.kind} · Original {formatCurrency(component.originalAmount, context.currency)} · Remaining {formatCurrency(component.remainingAmount, context.currency)}</p>
            {component.canRefund ? <>
              <NativeSelect id={`refund-component-${index}`} value={input.mode}
                onChange={(event) => onLine(component.componentId, { ...input, mode: event.target.value as RefundLineInput['mode'] })}>
                <option value="none">Do not refund</option>
                <option value="full">Full remaining component (including points)</option>
                {component.remainingAmount > 0 && <option value="amount">Partial amount</option>}
              </NativeSelect>
              {input.mode === 'amount' && <div className="space-y-1">
                <Label htmlFor={`refund-amount-${index}`}>Amount ({context.currency})</Label>
                <Input id={`refund-amount-${index}`} inputMode="decimal" value={input.amount}
                  onChange={(event) => onLine(component.componentId, { ...input, amount: event.target.value })} />
              </div>}
            </> : <p className="text-sm text-muted-foreground">{component.unavailableReason ?? 'This component cannot be refunded.'}</p>}
          </div>;
        })}
      </fieldset>
      {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
      {preview && <RefundPreviewDetails preview={preview} />}
      <DialogFooter>
        <Button variant="outline" disabled={busy} onClick={onClose}>Cancel</Button>
        <Button variant="outline" disabled={busy} onClick={onPreview}>{busy ? 'Working…' : 'Preview refund'}</Button>
        {preview && <Button disabled={busy} onClick={onConfirm}>Confirm refund request</Button>}
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}

export function RefundPreviewDetails({ preview }: { preview: RefundPreviewDto }) {
  return <section aria-label="Refund preview" className="space-y-3 rounded-md border border-border p-3">
    <h3 className="text-sm font-semibold">Refund preview</h3>
    <dl className="space-y-1 text-sm">
      <RefundFigure label="Total refund" value={formatCurrency(preview.total, preview.currency)} />
      <RefundFigure label="Cash refund" value={formatCurrency(preview.cashAmount, preview.currency)} />
      <RefundFigure label="Restore to original gift card" value={formatCurrency(preview.giftAmount, preview.currency)} />
      <RefundFigure label="Redeemed points to restore" value={String(preview.redeemedPointsToRestore)} />
      <RefundFigure label="Earned points to reverse" value={String(preview.earnedPointsToReverse)} />
    </dl>
    {preview.warnings.length > 0 && <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
      {preview.warnings.map((warning, index) => <li key={index}>{warning}</li>)}
    </ul>}
    <p className="text-xs text-muted-foreground">Confirmation submits a refund request. Its recorded status determines when the refund is complete. Fulfilment does not change.</p>
  </section>;
}

function RefundFigure({ label, value }: { label: string; value: string }) {
  return <div className="flex justify-between gap-3"><dt>{label}</dt><dd className="font-mono tabular-nums">{value}</dd></div>;
}
