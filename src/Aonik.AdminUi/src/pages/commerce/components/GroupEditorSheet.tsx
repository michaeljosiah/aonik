// Option-group editing and choice creation (Spec 074 §1). Together with ChoiceEditorSheet and
// CreateGroupDialog this closes the authoring loop, so a tenant can go from no catalogue to a
// servable group without leaving the admin surface.
//
// The update contract is the same split as the choice editor and matters more here, because
// `helpText` is the field most likely to be left alone: `label` and `helpText` are assigned
// UNCONDITIONALLY server-side, so both carry the full current text on every write. The value
// members (selectionMode, currency, sortOrder, isActive) preserve on omission — which is why
// currency is never sent from this sheet at all. It is not editable here, and an omitted one
// keeps what the group has; sending a guessed value is how a group gets re-denominated.

import { useState } from 'react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Pill } from '@/components/layout/aonik';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import type { OptionGroupDto } from '@/types/commerce';

import { SELECTION_MODES } from './selectionModes';

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

const CHOICE_KEY_PATTERN = /^[a-z0-9][a-z0-9-]*$/;

export function GroupEditorSheet({
  group,
  onClose,
  onSaved,
}: {
  group: OptionGroupDto;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [label, setLabel] = useState(group.label);
  const [helpText, setHelpText] = useState(group.helpText ?? '');
  const [selectionMode, setSelectionMode] = useState(group.selectionMode);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // New choice
  const [choiceKey, setChoiceKey] = useState('');
  const [choiceLabel, setChoiceLabel] = useState('');
  const [choicePrice, setChoicePrice] = useState('');
  const [addingChoice, setAddingChoice] = useState(false);

  const hasDefault = group.choices.some((c) => c.isActive && c.isRecommendedDefault);

  const saveGroup = async () => {
    if (!label.trim()) {
      setError('The group needs a label.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await commerceCatalogService.updateOptionGroup(group.id, {
        label: label.trim(),
        // Full text state, always — the server assigns this member whether or not it changed.
        helpText: helpText.trim() === '' ? null : helpText.trim(),
        ...(selectionMode === group.selectionMode ? {} : { selectionMode }),
      });
      toast.success('Group saved');
      onSaved();
      onClose();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The group could not be saved.');
    } finally {
      setSaving(false);
    }
  };

  const toggleRetired = async () => {
    setSaving(true);
    setError(null);
    try {
      await commerceCatalogService.updateOptionGroup(group.id, {
        label: group.label,
        helpText: group.helpText,
        isActive: !group.isActive,
      });
      toast.success(group.isActive ? 'Group retired' : 'Group reactivated');
      onSaved();
      onClose();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The group could not be updated.');
    } finally {
      setSaving(false);
    }
  };

  const addChoice = async () => {
    const key = choiceKey.trim().toLowerCase();
    if (!CHOICE_KEY_PATTERN.test(key)) {
      setError('The choice key is lower-case letters, digits and hyphens.');
      return;
    }
    if (!choiceLabel.trim()) {
      setError('The choice needs a label.');
      return;
    }
    // Blank is not zero — the same trap the choice editor guards. A group's prices are
    // absolute, so an unintended 0 here becomes the baseline everything else derives from.
    if (choicePrice.trim() === '') {
      setError('Enter the absolute price for this choice.');
      return;
    }
    const price = Number(choicePrice);
    if (!Number.isFinite(price) || price < 0) {
      setError('The price must be a number and cannot be negative.');
      return;
    }

    setAddingChoice(true);
    setError(null);
    try {
      await commerceCatalogService.addOptionChoice(group.id, {
        key,
        label: choiceLabel.trim(),
        price,
        // The FIRST active choice becomes the recommended default, because a group without
        // one is unservable — it would be created and then silently never shown.
        isRecommendedDefault: !hasDefault,
      });
      toast.success(hasDefault ? 'Choice added' : 'Choice added as the recommended default');
      setChoiceKey('');
      setChoiceLabel('');
      setChoicePrice('');
      onSaved();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The choice could not be added.');
    } finally {
      setAddingChoice(false);
    }
  };

  const busy = saving || addingChoice;

  return (
    <Sheet open onOpenChange={(open) => !open && !busy && onClose()}>
      <SheetContent size="md">
        <SheetHeader title={group.label} subtitle={group.key} />

        <SheetBody>
          {error && (
            <p className="mb-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
              {error}
            </p>
          )}

          {!hasDefault && (
            <p className="mb-3 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-[12px] text-[var(--color-warning)]">
              This group has no active recommended default, so the storefront shows it to nobody.
              Add a choice below to make it servable.
            </p>
          )}

          <fieldset disabled={busy} className="flex min-w-0 flex-col gap-4 border-0 p-0">
            <label className="flex flex-col gap-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Label
              </span>
              <input value={label} onChange={(e) => setLabel(e.target.value)} className={inputClass} />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Help text
              </span>
              <input
                value={helpText}
                onChange={(e) => setHelpText(e.target.value)}
                placeholder="Optional"
                className={inputClass}
              />
            </label>

            <label className="flex flex-col gap-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Selection
              </span>
              <select
                value={selectionMode}
                onChange={(e) => setSelectionMode(e.target.value)}
                className={inputClass}
              >
                {/* A mode the server holds but this list does not know still renders, so
                    saving can never silently rewrite it. */}
                {!SELECTION_MODES.some((m) => m.value === selectionMode) && (
                  <option value={selectionMode}>{selectionMode}</option>
                )}
                {SELECTION_MODES.map((mode) => (
                  <option key={mode.value} value={mode.value}>
                    {mode.label}
                  </option>
                ))}
              </select>
            </label>

            <p className="text-[11px] text-[var(--color-text-tertiary)]">
              Prices in {group.currency}. The currency is not editable here — changing it would
              re-denominate every stored price without converting any of them.
            </p>

            <div className="border-t border-[var(--color-border-light)] pt-3">
              <p className="mb-2 text-[12px] font-medium text-[var(--color-text-primary)]">
                Add a choice
              </p>
              <div className="flex flex-col gap-2">
                <input
                  value={choiceKey}
                  onChange={(e) => setChoiceKey(e.target.value)}
                  placeholder="Key, e.g. hot"
                  className={`${inputClass} font-[family-name:var(--font-mono)]`}
                />
                <input
                  value={choiceLabel}
                  onChange={(e) => setChoiceLabel(e.target.value)}
                  placeholder="Label, e.g. Hot"
                  className={inputClass}
                />
                <input
                  value={choicePrice}
                  onChange={(e) => setChoicePrice(e.target.value)}
                  inputMode="decimal"
                  placeholder={`Absolute price in ${group.currency}`}
                  className={`${inputClass} font-[family-name:var(--font-mono)]`}
                />
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="sm" onClick={() => void addChoice()}>
                    {addingChoice ? 'Adding…' : 'Add choice'}
                  </Button>
                  {!hasDefault && (
                    <Pill tone="info" size="sm">
                      becomes the default
                    </Pill>
                  )}
                </div>
              </div>
            </div>
          </fieldset>
        </SheetBody>

        <SheetFooter>
          <Button variant="outline" onClick={() => void toggleRetired()} disabled={busy}>
            {group.isActive ? 'Retire group' : 'Reactivate group'}
          </Button>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={() => void saveGroup()} disabled={busy}>
            {saving ? 'Saving…' : 'Save group'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
