// Combination-variant authoring (Spec 075 §2).
//
// A variant is keyed by its complete canonical selection, and the SERVER canonicalises
// (Spec 066) — this sheet sends a possibly-partial selection and lets the service normalise and
// store it complete. Canonicalising here would be a second implementation of the rule that
// decides variant identity, and a drift between the two would author a variant matching nothing.
//
// The SHAPE of each selection value is this sheet's business, though, and it is not free:
// `ResolveAsync` rejects a bare string for a `Multi` group (V5) and an array for a `One` group.
// That is why the controls differ by mode and the payload goes through `serialiseSelection`.
//
// Declarations left empty mean WITHHELD for this combination. Not inherited from the default
// block, not derived — the reason variants exist is that a salmon combination must never
// surface the standard preparation's shellfish line.

import { useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceContentService } from '@/services/commerceContentService';
import type { EffectiveOptionGroupDto, ProductContentVariantDto } from '@/types/commerce';

import { ContentFields } from './ContentFields';
import {
  draftFromVariant,
  emptyDraft,
  validateDraft,
  wireFromDraft,
  type ContentDraft,
} from '../lib/contentDraft';
import {
  describeDrift,
  detectSelectionDrift,
  hasDrift,
  isMulti,
  parseSelection,
  pickedGroupCount,
  serialiseSelection,
  toggleMulti,
  type SelectionValue,
} from '../lib/variantSelection';

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

interface ContentVariantSheetProps {
  productId: string;
  /** The product's effective offer — the only selections a variant can legitimately describe. */
  groups: EffectiveOptionGroupDto[];
  /** Editing an existing ACTIVE variant, or null when authoring (including reviving a retired one). */
  variant: ProductContentVariantDto | null;
  /** Pre-filled selection — from a coverage gap, or from a retired variant being revived. */
  initialSelectionJson?: string | null;
  onClose: () => void;
  onSaved: () => void;
}

export function ContentVariantSheet({
  productId,
  groups: initialGroups,
  variant,
  initialSelectionJson,
  onClose,
  onSaved,
}: ContentVariantSheetProps) {
  const [selection, setSelection] = useState<Record<string, SelectionValue>>(() =>
    parseSelection(variant?.selectionJson ?? initialSelectionJson ?? null),
  );
  const [draft, setDraft] = useState<ContentDraft>(() =>
    variant ? draftFromVariant(variant) : emptyDraft(),
  );
  const [saving, setSaving] = useState(false);
  const [reloading, setReloading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [baseline, setBaseline] = useState<ProductContentVariantDto | null>(variant);
  /**
   * The offer the CHIPS are drawn from, seeded by the page and replaced by the fresh read the
   * save performs.
   *
   * Refusing a save because the product gained a group, while still drawing the chips from the
   * snapshot that predates it, would be a dead end: the operator would be told to pick
   * something the sheet does not show. Adopting the fresh offer makes the refusal actionable on
   * the paths where the selection is still editable.
   */
  const [groups, setGroups] = useState<EffectiveOptionGroupDto[]>(initialGroups);

  // The sheet stays mounted after a conflict, so nothing re-initialises on its own. Without
  // this the operator was left with Save disabled over stale state and no way forward except
  // discovering that closing and reopening was the real reload.
  const reload = async () => {
    setReloading(true);
    setError(null);
    try {
      const fresh = await commerceContentService.getAdminContent(productId);
      const server = baseline ? fresh.variants.find((v) => v.id === baseline.id) : null;
      if (baseline && !server) {
        setError('This combination no longer exists. Close the sheet — there is nothing to edit.');
        return;
      }
      if (server && !server.isActive) {
        // The admin read RETAINS retired rows, so finding one is not proof it is editable.
        // UpdateVariantAsync rejects every edit to an inactive row with V-C5, so clearing the
        // conflict here would re-enable a Save that can only fail.
        setError(
          'This combination was retired while the sheet was open. Close it and use ' +
            '\u201cRe-author to revive\u201d — retired rows are history and cannot be edited.',
        );
        onSaved();
        return;
      }
      if (server) {
        setBaseline(server);
        setDraft(draftFromVariant(server));
        setSelection(parseSelection(server.selectionJson));
        setConflict(false);
      }
      onSaved();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The latest combination could not be read.');
    } finally {
      setReloading(false);
    }
  };

  const save = async () => {
    const invalid = validateDraft(draft);
    if (invalid) {
      setError(invalid);
      return;
    }
    if (groups.length === 0) {
      // Serialising against an empty group list yields `{}`, which the service reads as the
      // standard preparation and rejects with V-C1 — so an unreadable offer must stop the save
      // rather than produce a payload that means something else entirely.
      setError(
        'This product’s option offer could not be read, so a combination cannot be saved — the ' +
          'selection would be sent empty. Close and retry once the offer loads.',
      );
      return;
    }
    if (pickedGroupCount(selection) === 0) {
      setError('Pick at least one choice — a variant is identified by the combination it describes.');
      return;
    }
    // DRIFT — three ways a STORED selection can no longer be expressed, all silent otherwise:
    // a removed group is dropped by serialisation, a withdrawn choice is passed through to be
    // rejected, and a group that changed Multi→One truncates a multi-choice selection to its
    // first member. The last is the worst because nothing looks wrong: every group and choice
    // is still valid, the update accepts selection changes, and the variant lands on a
    // different combination carrying content authored for the original one.
    //
    // Applies to REVIVE as well as edit: a retired variant re-authored through
    // `initialSelectionJson` has no baseline, and that is the path where a bad save SUCCEEDS.
    setSaving(true);
    setError(null);
    setConflict(false);
    try {
      // The OFFER is re-read here, not taken from the page-load snapshot this sheet was given.
      // Whether a save lands on the combination it names is decided by the offer at write time,
      // and an option write by another operator changes that without touching anything this
      // sheet versions. Serialisation uses the fresh offer for the same reason: shaping a
      // payload against a snapshot is how it stops matching.
      let offer: EffectiveOptionGroupDto[];
      try {
        const product = await commerceCatalogService.getProduct(productId);
        offer = product.effectiveOptionGroups ?? [];
      } catch {
        // Fails CLOSED. An unread offer is not an unchanged one, and the whole point of the
        // check is that a moved offer is invisible in everything else the save can see.
        setError(
          'The product’s current options could not be read, so this cannot be saved safely — ' +
            'the combination it would land on cannot be confirmed. Try again.',
        );
        setSaving(false);
        return;
      }

      // DRIFT — three ways a stored selection can stop being expressible (removed group,
      // withdrawn choice, changed shape) plus one that is an ABSENCE: a group the product has
      // gained, which server normalisation fills with its default.
      //
      // `storedIsCanonical` covers every selection that came FROM the server, not only the
      // editing path. A retired variant revived through `initialSelectionJson` is server-stored
      // and complete, and the sheet presents it as a specific combination — the prefill is a
      // promise about identity, and a save that quietly lands elsewhere breaks it just as badly
      // as one from the edit path. Only a blank start is genuinely partial: nothing has been
      // promised there, so letting the server complete it is the intent.
      setGroups(offer);
      const drift = detectSelectionDrift(selection, offer, {
        storedIsCanonical: !!variant || !!initialSelectionJson,
      });
      if (hasDrift(drift)) {
        setConflict(true);
        setError(
          `This combination can no longer be expressed against what the product offers — ` +
            `${describeDrift(drift)}. Saving would move it onto a different combination, so it ` +
            'cannot be saved as it stands.',
        );
        setSaving(false);
        return;
      }

      if (baseline) {
        // The update is a FULL REPLACE and the service reloads the row inside its serialized
        // attempt, so serialization only ORDERS two saves — it does not detect that the second
        // is stale. Without this check a later editor silently erases the first one's figures,
        // allergens or heating. Same guard as the default block, which had it from the start
        // and this path did not.
        const fresh = await commerceContentService.getAdminContent(productId);
        const server = fresh.variants.find((v) => v.id === baseline.id);
        if (!server) {
          setConflict(true);
          setError('This combination no longer exists — it may have been retired. Reload.');
          setSaving(false);
          return;
        }
        if (signatureOf(server) !== signatureOf(baseline)) {
          setConflict(true);
          setError(
            'Someone else edited this combination while it was open. Reload to see their ' +
              'version — saving now would replace every field, including declarations.',
          );
          setSaving(false);
          return;
        }
      }

      const payload = {
        // Possibly PARTIAL, and shaped per group mode. The service normalises through Spec 066
        // and stores the complete canonical selection.
        selectionJson: serialiseSelection(selection, offer),
        ...wireFromDraft(draft),
      };
      if (baseline) await commerceContentService.updateVariant(baseline.id, payload);
      else await commerceContentService.upsertVariant(productId, payload);
      toast.success(baseline ? 'Combination saved' : 'Combination authored');
      onSaved();
      onClose();
    } catch (err: unknown) {
      // V-C2 (a variant must publish every figure the default publishes) and V5 (selection
      // shape) name specifics, so the message is shown verbatim rather than summarised.
      setError(readMessage(err) || 'The combination could not be saved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open onOpenChange={(open) => !open && !saving && onClose()}>
      <SheetContent size="md">
        <SheetHeader
          title={baseline ? 'Edit combination' : 'Author a combination'}
          subtitle="Declarations left empty are withheld for this combination — never inherited"
        />

        <SheetBody>
          {error && (
            <div className="mb-3 flex items-start gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
              <AlertCircle className="mt-px h-4 w-4 shrink-0" aria-hidden />
              <span className="flex-1">{error}</span>
              {conflict && (
                <button
                  type="button"
                  onClick={() => void reload()}
                  disabled={reloading}
                  className="shrink-0 underline"
                >
                  {reloading ? 'Reloading…' : 'Reload'}
                </button>
              )}
            </div>
          )}

          <fieldset disabled={saving || reloading} className="flex min-w-0 flex-col gap-4 border-0 p-0">
            <div>
              <p className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Combination
              </p>
              {groups.length === 0 ? (
                <p className="text-[12px] text-[var(--color-text-secondary)]">
                  This product offers no option groups, so it has no combinations to describe.
                </p>
              ) : (
                <div className="flex flex-col gap-2.5">
                  {groups.map((group) =>
                    isMulti(group) ? (
                      <MultiGroup
                        key={group.key}
                        group={group}
                        value={selection[group.key]}
                        disabled={!!baseline}
                        onToggle={(choiceKey) =>
                          setSelection({
                            ...selection,
                            [group.key]: toggleMulti(selection[group.key], choiceKey),
                          })
                        }
                      />
                    ) : (
                      <label key={group.key} className="flex items-center gap-2">
                        <span className="w-[130px] shrink-0 text-[12px] text-[var(--color-text-secondary)]">
                          {group.label ?? group.key}
                        </span>
                        <select
                          value={asSingle(selection[group.key])}
                          onChange={(e) =>
                            setSelection({ ...selection, [group.key]: e.target.value })
                          }
                          className={inputClass}
                          // The selection IS the key, so editing must not retarget authored
                          // content at a different combination.
                          disabled={!!variant}
                        >
                          <option value="">— not specified —</option>
                          {group.choices.map((choice) => (
                            <option key={choice.key} value={choice.key}>
                              {choice.label}
                            </option>
                          ))}
                        </select>
                      </label>
                    ),
                  )}
                </div>
              )}
              {baseline && (
                <p className="mt-1.5 flex items-center gap-1.5 text-[11px] text-[var(--color-text-tertiary)]">
                  <Pill tone="muted" size="sm">
                    fixed
                  </Pill>
                  A variant is identified by its combination — retire it and author another to
                  describe a different one.
                </p>
              )}
            </div>

            <ContentFields draft={draft} onChange={setDraft} />
          </fieldset>
        </SheetBody>

        <SheetFooter>
          <Button variant="outline" onClick={onClose} disabled={saving || reloading}>
            Cancel
          </Button>
          <Button
            onClick={() => void save()}
            disabled={saving || reloading || conflict || groups.length === 0}
          >
            {saving ? 'Saving…' : 'Save combination'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function MultiGroup({
  group,
  value,
  disabled,
  onToggle,
}: {
  group: EffectiveOptionGroupDto;
  value: SelectionValue | undefined;
  disabled: boolean;
  onToggle: (choiceKey: string) => void;
}) {
  const picked = Array.isArray(value) ? value : value ? [value] : [];
  return (
    <div className="flex items-start gap-2">
      <span className="mt-1 w-[130px] shrink-0 text-[12px] text-[var(--color-text-secondary)]">
        {group.label ?? group.key}
        <span className="ml-1 text-[10px] text-[var(--color-text-tertiary)]">any</span>
      </span>
      <div className="flex flex-wrap gap-1.5">
        {group.choices.map((choice) => (
          <button
            key={choice.key}
            type="button"
            disabled={disabled}
            onClick={() => onToggle(choice.key)}
            className={[
              'rounded-full border px-2.5 py-1 text-[11.5px]',
              picked.includes(choice.key)
                ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)]/10 text-[var(--color-text-primary)]'
                : 'border-dashed border-[var(--color-border)] text-[var(--color-text-tertiary)]',
              disabled ? 'cursor-default' : 'cursor-pointer',
            ].join(' ')}
          >
            {choice.label}
          </button>
        ))}
      </div>
    </div>
  );
}

function asSingle(value: SelectionValue | undefined): string {
  if (value === undefined) return '';
  return Array.isArray(value) ? (value[0] ?? '') : value;
}

/** Everything an update would overwrite, for the staleness comparison. */
function signatureOf(variant: ProductContentVariantDto): string {
  return JSON.stringify([
    // The SELECTION is mutable through the same update API, so a concurrent retarget that
    // changed no content field read as "fresh" and this sheet then moved the variant back.
    // Identity belongs in the signature exactly as much as the content does.
    variant.selectionJson,
    variant.servingLabel,
    variant.nutrition,
    variant.ingredients,
    variant.allergens,
    variant.heating,
    variant.isActive,
  ]);
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
