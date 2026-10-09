import { useState, type FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Switch } from '@/components/ui/switch';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { commerceDiscountService } from '@/services/commerceDiscountService';
import type { DiscountDto, DiscountKind } from '@/types/commerce';
import { discountCreateFrom, discountFormFrom, discountUpdateFrom, type DiscountForm } from '../lib/discountForm';
import { DiscountProductSelection } from './DiscountProductSelection';

export function DiscountEditorDialog({ discount, onClose, onSaved, onReload }: {
  discount?: DiscountDto;
  onClose: () => void;
  onSaved: () => void;
  onReload: () => void;
}) {
  const [form, setForm] = useState(() => discountFormFrom(discount));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const change = <K extends keyof DiscountForm>(key: K, value: DiscountForm[K]) =>
    setForm((previous) => ({ ...previous, [key]: value }));

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (saving || conflict) return;
    setError(null);
    try {
      if (discount) {
        const request = discountUpdateFrom(form, discount);
        setSaving(true);
        await commerceDiscountService.update(discount.id, request);
      } else {
        const request = discountCreateFrom(form);
        setSaving(true);
        await commerceDiscountService.create(request);
      }
      toast.success(discount ? 'Discount code updated' : 'Discount code created');
      onSaved();
      onClose();
    } catch (err: unknown) {
      if (isAxiosError(err) && err.response?.status === 409) {
        setConflict(true);
        setError('This discount code changed or now conflicts with another code. Your draft has not been saved. Reload the list and open the latest version before editing again.');
      } else {
        setError(err && typeof err === 'object' && 'userMessage' in err
          ? String(err.userMessage) : err instanceof Error ? err.message : 'The discount code could not be saved.');
      }
    } finally { setSaving(false); }
  };

  return (
    <Dialog open onOpenChange={(open) => { if (!open && !saving) onClose(); }}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{discount ? `Edit ${discount.code}` : 'New discount code'}</DialogTitle>
          <DialogDescription>Discounts apply to eligible goods. Delivery and gift card value are excluded.</DialogDescription>
        </DialogHeader>
        <form onSubmit={(event) => void save(event)} className="space-y-4">
          {error && <Alert variant="destructive"><AlertDescription>
            {error}
            {conflict && <Button type="button" variant="outline" size="sm" className="mt-2" onClick={() => { onClose(); onReload(); }}>
              Discard draft and reload list
            </Button>}
          </AlertDescription></Alert>}
          <fieldset disabled={saving} className="min-w-0 space-y-4 border-0 p-0">
            <div className="space-y-1">
              <Label htmlFor="discount-code">Code</Label>
              <Input id="discount-code" value={form.code} readOnly={!!discount} maxLength={64} required
                onChange={(e) => change('code', e.target.value)} autoComplete="off" />
              <p className="text-xs text-muted-foreground">The code cannot be changed after creation.</p>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="discount-kind">Kind</Label>
                <NativeSelect id="discount-kind" value={form.kind} onChange={(e) => change('kind', e.target.value as DiscountKind)}>
                  <option value="Percentage">Percentage</option><option value="FixedAmount">Fixed amount</option>
                </NativeSelect>
              </div>
              <div className="space-y-1">
                <Label htmlFor="discount-value">{form.kind === 'Percentage' ? 'Percentage (up to 100%)' : 'Amount'}</Label>
                <Input id="discount-value" inputMode="decimal" value={form.value} required onChange={(e) => change('value', e.target.value)} />
              </div>
            </div>
            {form.kind === 'FixedAmount' && <div className="space-y-1">
              <Label htmlFor="discount-currency">Currency code</Label>
              <Input id="discount-currency" value={form.currency} required maxLength={3} placeholder="e.g. GBP"
                onChange={(e) => change('currency', e.target.value)} />
              <p className="text-xs text-muted-foreground">The fixed discount can only be used for orders in this currency.</p>
            </div>}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="discount-expiry">Expires at (UTC)</Label>
                <Input id="discount-expiry" type="datetime-local" step="any" value={form.expiresAtUtc}
                  onChange={(e) => change('expiresAtUtc', e.target.value)} />
                <p className="text-xs text-muted-foreground">Blank means no expiry.</p>
              </div>
              <div className="space-y-1">
                <Label htmlFor="discount-limit">Maximum redemptions</Label>
                <Input id="discount-limit" inputMode="numeric" value={form.maxRedemptions}
                  onChange={(e) => change('maxRedemptions', e.target.value)} />
                <p className="text-xs text-muted-foreground">Blank means no limit.</p>
              </div>
            </div>
            {discount ? <div className="space-y-2">
              <div className="flex items-center gap-2"><Switch id="discount-active" checked={form.isActive}
                onCheckedChange={(checked) => change('isActive', checked)} disabled={saving} />
                <Label htmlFor="discount-active">Active</Label></div>
              <p className="text-xs text-muted-foreground">{discount.timesRedeemed} completed · {discount.reservedCount} reserved. Existing payment attempts keep their agreed discount.</p>
            </div> : <p className="text-xs text-muted-foreground">New codes are active immediately.</p>}
            <div className="space-y-2">
              <Label htmlFor="discount-eligibility">Product eligibility</Label>
              <NativeSelect id="discount-eligibility" value={form.selectedProductsOnly ? 'selected' : 'all'}
                onChange={(e) => change('selectedProductsOnly', e.target.value === 'selected')}>
                <option value="all">All eligible goods</option><option value="selected">Selected products</option>
              </NativeSelect>
              {form.selectedProductsOnly && <DiscountProductSelection selectedIds={form.productIds}
                onChange={(ids) => change('productIds', ids)} disabled={saving} />}
            </div>
          </fieldset>
          <DialogFooter>
            <Button type="button" variant="outline" disabled={saving} onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={saving || conflict}>{saving ? 'Saving…' : discount ? 'Save changes' : 'Create code'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
