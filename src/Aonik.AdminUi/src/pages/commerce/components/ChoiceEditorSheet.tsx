// Choice editor (Spec 074 §2 — the row's Edit action). Label, note, absolute price.
//
// The update contract splits its members: `label` and `note` are assigned UNCONDITIONALLY
// server-side, so both must carry the full current text or the omitted one is erased; the
// value-typed members preserve on omission. The request type encodes that (`note: string |
// null` is required), and this sheet always sends both.
//
// The price field is the ABSOLUTE per-unit amount (Spec 066 §8), captioned as such — an
// operator who reads it as "the extra" would author a catalogue whose every delta is wrong.

import { useState } from 'react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceCatalogService } from '@/services/commerceCatalogService';

import { validateSurchargeAmount } from '../lib/productForm';
import type { OptionChoiceDto, OptionGroupDto } from '@/types/commerce';

import { choiceDelta, effectiveDefaultChoice } from '../lib/optionPricing';
import { SignedAmount } from './SignedAmount';

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

export function ChoiceEditorSheet({
  group,
  choice,
  onClose,
  onSaved,
}: {
  group: OptionGroupDto;
  choice: OptionChoiceDto;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [label, setLabel] = useState(choice.label);
  const [note, setNote] = useState(choice.note ?? '');
  const [price, setPrice] = useState(String(choice.price));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const groupDefault = effectiveDefaultChoice(group.choices);
  const blankPrice = price.trim() === '';
  const parsed = blankPrice ? Number.NaN : Number(price);
  const editingTheDefault = groupDefault?.key === choice.key;
  // Editing the DEFAULT moves the baseline with it — it is its own zero point, so its own
  // delta stays 0 no matter what the new price is. Comparing the new price against the old
  // one would show "+1.50 against" the very choice being edited.
  const previewBaseline = editingTheDefault ? { price: parsed } : groupDefault;
  const previewDelta =
    Number.isFinite(parsed) && previewBaseline ? choiceDelta({ price: parsed }, previewBaseline) : null;

  const save = async () => {
    if (!label.trim()) {
      setError('A choice needs a label.');
      return;
    }
    // Checked BEFORE the numeric conversion: Number('') is 0, so a cleared field would pass
    // a finite/non-negative test and silently reprice the choice to zero — which, on a
    // recommended default, silently rewrites every derived delta in the catalogue.
    if (blankPrice) {
      setError('Enter the absolute price. Clearing the field is not the same as pricing it at zero.');
      return;
    }
    // Same rule as the product surcharge (Spec 082): shape, sign, SCALE and width, decided on
    // the text. OptionChoice.Price is decimal(19,4), so a fifth decimal is not rejected by the
    // database — it is rounded, and the sheet would report success for one amount while a
    // reload showed another.
    const priceError = validateSurchargeAmount(price);
    if (priceError) {
      setError(priceError);
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await commerceCatalogService.updateOptionChoice(choice.id, {
        label: label.trim(),
        // Sent as the empty-to-null it is: the server assigns this member unconditionally, so
        // an omitted note is a deleted note.
        note: note.trim() === '' ? null : note.trim(),
        // Value-typed members preserve on omission, so an UNCHANGED price is left out — a
        // resent one would revert a concurrent repricing by another admin that this sheet
        // never saw.
        ...(parsed === choice.price ? {} : { price: parsed }),
      });
      toast.success('Choice saved');
      onSaved();
      onClose();
    } catch (err: unknown) {
      setError(
        (err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '') || 'The choice could not be saved.',
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open onOpenChange={(open) => !open && !saving && onClose()}>
      <SheetContent size="md">
        <SheetHeader title={choice.label} subtitle={`${group.label} — ${choice.key}`} />

        <SheetBody>
          {error && (
            <p className="mb-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
              {error}
            </p>
          )}

          <fieldset disabled={saving} className="flex min-w-0 flex-col gap-4 border-0 p-0">
            <label className="flex flex-col gap-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Label
              </span>
              <input value={label} onChange={(e) => setLabel(e.target.value)} className={inputClass} />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Note
              </span>
              <input
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Optional"
                className={inputClass}
              />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Price ({group.currency})
              </span>
              <input
                value={price}
                onChange={(e) => setPrice(e.target.value)}
                inputMode="decimal"
                className={`${inputClass} font-[family-name:var(--font-mono)]`}
              />
              <span className="mt-0.5 flex items-center gap-1.5 text-[11px] text-[var(--color-text-tertiary)]">
                This is the ABSOLUTE per-unit price, not the extra (Spec 066 §8).
                {editingTheDefault ? (
                  <>This choice IS the default, so it is its own baseline and always reads 0.</>
                ) : (
                  groupDefault &&
                  previewDelta !== null && (
                    <>
                      Against {groupDefault.label} that reads{' '}
                      <SignedAmount amount={previewDelta} currency={group.currency} />
                    </>
                  )
                )}
              </span>
            </label>
          </fieldset>
        </SheetBody>

        <SheetFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving}>
            {saving ? 'Saving…' : 'Save choice'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
