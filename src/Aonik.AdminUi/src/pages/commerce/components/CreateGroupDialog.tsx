// Create an option group (Spec 074 §3 — the empty state's CTA).
//
// Bootstrapping matters more than it looks: with no groups, nothing on the storefront can be
// personalised at all, and until this existed the only route was the CLI. A tenant cannot be
// expected to leave the admin surface to make the admin surface usable.
//
// The KEY is immutable after create and is what every product narrowing line references, so it
// is authored deliberately rather than derived from the label — a slugged label would change
// under a rename and quietly orphan the lines that point at it.

import { useState } from 'react';
import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { commerceCatalogService } from '@/services/commerceCatalogService';

import { SELECTION_MODES } from './selectionModes';

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

const KEY_PATTERN = /^[a-z0-9][a-z0-9-]*$/;

export function CreateGroupDialog({
  defaultCurrency,
  onClose,
  onCreated,
}: {
  defaultCurrency: string | null;
  onClose: () => void;
  onCreated: () => void;
}) {
  const [key, setKey] = useState('');
  const [label, setLabel] = useState('');
  const [selectionMode, setSelectionMode] = useState(SELECTION_MODES[0].value);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const create = async () => {
    const trimmedKey = key.trim().toLowerCase();
    if (!KEY_PATTERN.test(trimmedKey)) {
      setError('The key is lower-case letters, digits and hyphens — and cannot be changed later.');
      return;
    }
    if (!label.trim()) {
      setError('The group needs a label for the storefront.');
      return;
    }
    // The endpoint substitutes the LITERAL "GBP" for an omitted currency
    // (CreateOptionGroupEndpoint.cs:31), so omitting it is not a tenant-aware default — it is
    // a guess that denominates every absolute price in this group wrongly and then fails
    // cross-currency validation at quote time. Without a known currency there is nothing safe
    // to send, so the dialog refuses rather than creating a group that has to be deleted.
    if (!defaultCurrency) {
      setError(
        'The storefront currency could not be read, so a group cannot be created yet — its ' +
          'prices would be denominated by a fallback rather than by your storefront. Reopen ' +
          'this page to retry.',
      );
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await commerceCatalogService.createOptionGroup({
        key: trimmedKey,
        label: label.trim(),
        selectionMode,
        currency: defaultCurrency,
      });
      toast.success('Group created — add its choices next');
      onCreated();
      onClose();
    } catch (err: unknown) {
      setError(
        (err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '') || 'The group could not be created.',
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && !saving && onClose()}>
      <DialogContent className="sm:max-w-[460px]">
        <DialogHeader>
          <DialogTitle>New option group</DialogTitle>
          <DialogDescription>
            A group is offered to products by narrowing; it shows nothing on the storefront until
            it has active choices and one recommended default.
          </DialogDescription>
        </DialogHeader>

        {error && (
          <p className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
            {error}
          </p>
        )}

        <fieldset disabled={saving} className="flex min-w-0 flex-col gap-3 border-0 p-0">
          <label className="flex flex-col gap-1">
            <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
              Key
            </span>
            <input
              value={key}
              onChange={(e) => setKey(e.target.value)}
              placeholder="spice-level"
              className={`${inputClass} font-[family-name:var(--font-mono)]`}
            />
            <span className="text-[11px] text-[var(--color-text-tertiary)]">
              Immutable after create — every product narrowing refers to it.
            </span>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
              Label
            </span>
            <input
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder="Spice level"
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
              {SELECTION_MODES.map((mode) => (
                <option key={mode.value} value={mode.value}>
                  {mode.label}
                </option>
              ))}
            </select>
          </label>

          <p
            className={`text-[11px] ${
              defaultCurrency
                ? 'text-[var(--color-text-tertiary)]'
                : 'text-[var(--color-warning)]'
            }`}
          >
            {defaultCurrency
              ? `Prices in ${defaultCurrency}, from the storefront configuration.`
              : 'The storefront currency is unknown, so a group cannot be created right now.'}
          </p>
        </fieldset>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={() => void create()} disabled={saving || !defaultCurrency}>
            {saving ? 'Creating…' : 'Create group'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
