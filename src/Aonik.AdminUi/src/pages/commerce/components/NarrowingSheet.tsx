// Per-product narrowing (Spec 074 §2). Built from the RAW stored lines, never from the
// resolved effective view, because the resolved view has already thrown away the one bit that
// matters most here.
//
// `allowedChoiceKeys: null` means INHERIT every active choice — including choices added to the
// catalogue tomorrow. An explicit list means exactly these, forever, until edited. Those are
// different promises, they look identical once resolved, and saving a resolved view silently
// converts every inherited product into a pinned one. So the toggle is modelled directly: on =
// null, off = the current set frozen into a list the operator then edits chip by chip.
//
// Saving performs a FULL REPLACE with exactly what is on screen. A save modelled on anything
// less — sending only touched groups, say — would leave untouched groups intact and quietly
// widen the product relative to what the operator was looking at.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, Star } from 'lucide-react';
import { toast } from 'sonner';

import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceCatalogService, type ProductOptionGroupLine } from '@/services/commerceCatalogService';
import type { OptionGroupDto, ProductSummaryDto } from '@/types/commerce';

import { SignedAmount } from './SignedAmount';
import { choiceDelta, effectiveDefaultChoice } from '../lib/optionPricing';

/** One catalogue group as the operator is currently shaping it for this product. */
interface GroupDraft {
  included: boolean;
  /** True while `allowedChoiceKeys` is null — the inherit-future promise. */
  inherit: boolean;
  /** Only meaningful when `inherit` is false. */
  pinned: Set<string>;
  defaultChoiceKey: string | null;
  /**
   * Carried through UNEDITED. This sheet exposes no editor for it, but the save is a full
   * replace — so a field read and not resent is a field deleted, and dropping it would
   * quietly revert a multi-select group to the catalogue's mode.
   */
  selectionModeOverride: string | null;
  sortOrder: number;
}

interface NarrowingSheetProps {
  product: ProductSummaryDto;
  groups: OptionGroupDto[];
  /** Default for a FIRST-TIME surcharge only; never overrides a stored denomination. */
  storefrontCurrency: string | null;
  onClose: () => void;
  onSaved: () => void;
}

export function NarrowingSheet({
  product,
  groups,
  storefrontCurrency,
  onClose,
  onSaved,
}: NarrowingSheetProps) {
  const [drafts, setDrafts] = useState<Map<string, GroupDraft> | null>(null);
  const [amount, setAmount] = useState('');
  const [originalAmount, setOriginalAmount] = useState('');
  // The stored denomination, read with the product. The list summary carries the surcharge
  // NUMBER but not its currency, so taking the storefront's would silently redenominate a
  // product whose surcharge is legitimately held in another.
  const [storedCurrency, setStoredCurrency] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  // Reload fires load() AND the parent refresh, and the parent replaces `groups` — which
  // changes this callback and launches a SECOND load. Both used to commit unconditionally, so
  // an older response built from the previous catalogue could land last and win.
  const generationRef = useRef(0);

  const load = useCallback(async () => {
    const generation = generationRef.current + 1;
    generationRef.current = generation;
    const current = () => generationRef.current === generation;
    setLoading(true);
    setError(null);
    try {
      // Narrowing AND detail: the raw lines carry the offer, the detail carries the surcharge
      // with its own currency. Both are re-read on a conflict reload.
      const [lines, detail] = await Promise.all([
        commerceCatalogService.getProductNarrowing(product.id),
        commerceCatalogService.getProduct(product.id),
      ]);
      const next = new Map<string, GroupDraft>();
      for (const group of groups) {
        const line = lines.find((l) => l.groupKey === group.key);
        next.set(group.key, {
          included: !!line,
          // Absent line and inherited line both start with an empty pinned set; `inherit`
          // is what distinguishes them, and it is read straight from the raw value.
          inherit: line ? line.allowedChoiceKeys === null : true,
          pinned: new Set(line?.allowedChoiceKeys ?? []),
          defaultChoiceKey: line?.defaultChoiceKey ?? null,
          selectionModeOverride: line?.selectionModeOverride ?? null,
          sortOrder: line?.sortOrder ?? group.sortOrder,
        });
      }
      if (!current()) return;
      setDrafts(next);
      const initial = detail.unitSurcharge != null ? String(detail.unitSurcharge) : '';
      setAmount(initial);
      setOriginalAmount(initial);
      setStoredCurrency(detail.unitSurchargeCurrency ?? null);
      setConflict(false);
    } catch (err: unknown) {
      if (!current()) return;
      setDrafts(null);
      setError(readMessage(err) || 'This product’s options could not be read.');
    } finally {
      if (current()) setLoading(false);
    }
  }, [product.id, groups]);

  useEffect(() => {
    void load();
  }, [load]);

  const update = (groupKey: string, patch: Partial<GroupDraft>) => {
    setDrafts((current) => {
      if (!current) return current;
      const next = new Map(current);
      const draft = next.get(groupKey);
      if (draft) next.set(groupKey, { ...draft, ...patch });
      return next;
    });
  };

  const save = async () => {
    if (!drafts) return;

    // Validated BEFORE the first write. The option replace committed first previously, so a
    // rejected amount left the offer already changed under an error saying the save failed —
    // and neither correcting nor cancelling could undo it.
    const parsedAmount = amount.trim() === '' ? null : Number(amount);
    if (parsedAmount !== null && (!Number.isFinite(parsedAmount) || parsedAmount < 0)) {
      setError('The surcharge must be a number and cannot be negative.');
      return;
    }
    const surchargeCurrency = storedCurrency ?? storefrontCurrency;
    if (amount !== originalAmount && parsedAmount !== null && !surchargeCurrency) {
      setError('A surcharge needs a currency, and none is known for this product yet.');
      return;
    }

    // A pinned group whose effective default is not among its offered choices has no
    // resolvable default, and the backend rejects the whole replace. Caught here, naming the
    // group, rather than surfacing a payload-level error the operator has to decode.
    for (const group of groups) {
      const draft = drafts.get(group.key);
      if (!draft?.included || draft.inherit) continue;
      const offeredKeys = draft.pinned;
      if (offeredKeys.size === 0) {
        // An included group offering nothing is rejected outright by the backend, so the
        // sheet would simply never save. Named here instead.
        setError(`${group.label}: offer at least one choice, or exclude the group.`);
        return;
      }
      const defaultKey =
        draft.defaultChoiceKey ??
        group.choices.find((c) => c.isRecommendedDefault && c.isActive)?.key ??
        null;
      if (!defaultKey || !offeredKeys.has(defaultKey)) {
        setError(`${group.label}: pick a default from the choices this product offers.`);
        return;
      }
    }

    setSaving(true);
    setError(null);
    setConflict(false);
    let offerCommitted = false;
    try {
      // EXACTLY the visible intersection, every included group, in one replace.
      const lines: ProductOptionGroupLine[] = groups
        .filter((group) => drafts.get(group.key)?.included)
        .map((group) => {
          const draft = drafts.get(group.key)!;
          return {
            groupKey: group.key,
            allowedChoiceKeys: draft.inherit ? null : [...draft.pinned],
            defaultChoiceKey: draft.defaultChoiceKey,
            selectionModeOverride: draft.selectionModeOverride,
            sortOrder: draft.sortOrder,
          };
        });
      // TWO endpoints, so two commit points — there is no composite write. The offer can
      // land and the surcharge fail, so the error names what already changed rather than
      // reporting a save that partly succeeded as a save that failed.
      await commerceCatalogService.setProductOptionGroups(product.id, lines);
      offerCommitted = true;

      if (amount !== originalAmount) {
        await commerceCatalogService.setUnitSurcharge(
          product.id,
          parsedAmount,
          parsedAmount === null ? null : surchargeCurrency,
        );
      }

      toast.success('Offer saved');
      onSaved();
      onClose();
    } catch (err: unknown) {
      // The backend serialises full replaces on the product row and 409s the loser, which must
      // revalidate rather than retry blind — so this offers a reload, not a retry.
      const code = (err as { response?: { data?: { code?: string } } })?.response?.data?.code;
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 409 || code === 'concurrency_conflict') setConflict(true);
      const message = readMessage(err) || 'The offer could not be saved.';
      setError(
        offerCommitted
          ? `${message} The option groups were already saved; only the surcharge failed.`
          : message,
      );
      // A committed offer means the list behind the sheet is stale even though this failed.
      if (offerCommitted) onSaved();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open onOpenChange={(open) => !open && !saving && onClose()}>
      <SheetContent size="md">
        <SheetHeader title={product.name} subtitle={`${product.slug} — what this product offers`} />

        <SheetBody>
          {error && (
            <div className="mb-3 flex items-start gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
              <AlertCircle className="mt-px h-4 w-4 shrink-0" aria-hidden />
              <span className="flex-1">{error}</span>
              {conflict && (
                // Re-reads THIS sheet, not only the list behind it. Save stayed enabled while
                // the refresh was in flight, so a second click resent the stale replace and
                // overwrote the concurrent winner that caused the conflict.
                <button
                  type="button"
                  onClick={() => {
                    void load();
                    onSaved();
                  }}
                  className="shrink-0 underline"
                >
                  Reload
                </button>
              )}
            </div>
          )}

          {loading ? (
            <p className="py-8 text-center text-sm text-[var(--color-text-secondary)]">Loading…</p>
          ) : !drafts ? (
            <p className="py-8 text-center text-sm text-[var(--color-text-secondary)]">
              Nothing to edit — this product’s stored options could not be read.
            </p>
          ) : (
            <fieldset disabled={saving} className="flex min-w-0 flex-col gap-4 border-0 p-0">
              {groups.length === 0 ? (
                <p className="text-[12.5px] text-[var(--color-text-secondary)]">
                  There is no option catalogue yet, so there is nothing to offer.
                </p>
              ) : (
                groups.map((group) => {
                  // A group added to the catalogue after this sheet loaded has no draft yet;
                  // rendering it with `undefined` would crash rather than simply waiting for
                  // the reload that is already on its way.
                  const draft = drafts.get(group.key);
                  return draft ? (
                    <GroupSection
                      key={group.key}
                      group={group}
                      draft={draft}
                      onChange={(patch) => update(group.key, patch)}
                    />
                  ) : null;
                })
              )}

              <AonikCard title="Unit surcharge" padding={12}>
                <input
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  inputMode="decimal"
                  placeholder="None"
                  className="w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] outline-none focus:border-[var(--color-brand-primary)]"
                />
                <p className="mt-1.5 text-[11px] text-[var(--color-text-tertiary)]">
                  The one price-like field a product card may show
                  {(storedCurrency ?? storefrontCurrency)
                    ? ` — in ${storedCurrency ?? storefrontCurrency}.`
                    : '. A currency is required, and none is known yet.'}
                </p>
              </AonikCard>
            </fieldset>
          )}
        </SheetBody>

        <SheetFooter>
          <span className="mr-auto max-w-[300px] text-[11px] text-[var(--color-text-tertiary)]">
            Saving replaces this product’s whole offer with exactly what is shown above.
          </span>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving || loading || !drafts || conflict}>
            {saving ? 'Saving…' : 'Save offer'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

function GroupSection({
  group,
  draft,
  onChange,
}: {
  group: OptionGroupDto;
  draft: GroupDraft;
  onChange: (patch: Partial<GroupDraft>) => void;
}) {
  const activeChoices = useMemo(() => group.choices.filter((c) => c.isActive), [group.choices]);

  // RETIRED choices still pinned are shown, not filtered away. `draft.pinned` resends them, so
  // hiding one made the sheet claim a list it was not sending — and reactivating that choice
  // later would silently put it back on the product without anyone including it.
  const editableChoices = useMemo(
    () =>
      group.choices.filter(
        (c) =>
          c.isActive ||
          draft.pinned.has(c.key) ||
          // A retired choice held as the explicit default is hidden state too: the payload
          // resends it, and reactivating that choice would silently make it the product
          // default again. Shown so the operator decides.
          draft.defaultChoiceKey === c.key,
      ),
    [group.choices, draft.pinned, draft.defaultChoiceKey],
  );

  // The baseline is the EFFECTIVE default: a product-level override moves the zero point, so
  // the same choice reads differently here than in the catalogue table. Both are correct.
  const baseline = effectiveDefaultChoice(activeChoices, draft.defaultChoiceKey);

  // The choices this product actually offers right now — inherited means all active ones.
  const offered = draft.inherit
    ? activeChoices
    : activeChoices.filter((choice) => draft.pinned.has(choice.key));

  // A retired group, or one with nothing active in it, is dropped by ComposeEffective — so
  // including it saves cleanly and shows customers nothing. Already-stored lines stay
  // removable; only NEW inclusion is blocked.
  // The server's own rule (ProductOptionService.IsServable): active, at least one active
  // choice, and EXACTLY ONE active recommended default. A half-authored group with no default
  // is returned by the admin catalogue but dropped from every storefront composition, and an
  // inherited line on one fails V8 at save.
  const servable =
    group.isActive &&
    activeChoices.length > 0 &&
    activeChoices.filter((c) => c.isRecommendedDefault).length === 1;

  const togglePinned = (key: string) => {
    const next = new Set(draft.pinned);
    if (next.has(key)) next.delete(key);
    else next.add(key);

    // Excluding the effective default leaves the line with no resolvable default, which the
    // backend rejects. That holds for an INHERITED default too — my first pass only handled an
    // explicit one — so the check is on the effective key, and a remaining choice takes over.
    const effectiveKey = draft.defaultChoiceKey ?? baseline?.key ?? null;
    let defaultChoiceKey = draft.defaultChoiceKey;
    if (effectiveKey && !next.has(effectiveKey)) {
      const replacement = activeChoices.find((c) => next.has(c.key));
      defaultChoiceKey = replacement ? replacement.key : null;
    }
    onChange({ pinned: next, defaultChoiceKey });
  };

  return (
    <AonikCard padding={12}>
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={draft.included}
          disabled={!servable && !draft.included}
          onChange={(e) => onChange({ included: e.target.checked })}
        />
        <span className="text-[13px] font-medium text-[var(--color-text-primary)]">
          {group.label}
        </span>
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
          {group.key}
        </span>
        {!group.isActive && (
          <Pill tone="muted" size="sm">
            Retired
          </Pill>
        )}
        {!servable && !draft.included && (
          <span className="text-[11px] text-[var(--color-text-tertiary)]">
            not servable — customers would never see it
          </span>
        )}
      </label>

      {draft.included && (
        <div className="mt-2.5 flex flex-col gap-2">
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={draft.inherit}
              onChange={(e) =>
                onChange({
                  inherit: e.target.checked,
                  // Switching inheritance OFF pins what is on screen right now, so the product
                  // keeps offering exactly what it offered a moment ago — just frozen.
                  pinned: e.target.checked
                    ? draft.pinned
                    : new Set(activeChoices.map((c) => c.key)),
                  // KEEP a product default that is still valid. Clearing it unconditionally
                  // moved the product's standard preparation back to the catalogue
                  // recommendation — and could stage content review — when the operator had
                  // only asked to stop inheriting FUTURE choices. Only a default that is no
                  // longer active is dropped, because it could not survive the pin anyway.
                  defaultChoiceKey:
                    e.target.checked ||
                    activeChoices.some((c) => c.key === draft.defaultChoiceKey)
                      ? draft.defaultChoiceKey
                      : null,
                })
              }
            />
            <span className="text-[12px] text-[var(--color-text-secondary)]">
              All active choices (inherited)
            </span>
          </label>

          <p className="text-[11px] text-[var(--color-text-tertiary)]">
            {draft.inherit
              ? 'Choices added to this group later will be offered here automatically.'
              : 'Pinned: only the choices selected below, now and in future.'}
          </p>

          <div className="flex flex-wrap gap-1.5">
            {editableChoices.map((choice) => {
              const offered = draft.inherit || draft.pinned.has(choice.key);
              const delta = choiceDelta(choice, baseline);
              const isDefault = baseline?.key === choice.key;
              return (
                <button
                  key={choice.key}
                  type="button"
                  // Read-only while inherited: the set is not the operator's to edit until they
                  // take the inherit promise off, and a clickable chip would imply otherwise.
                  disabled={draft.inherit}
                  onClick={() => togglePinned(choice.key)}
                  className={[
                    'flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11.5px]',
                    offered
                      ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)]/10 text-[var(--color-text-primary)]'
                      : 'border-dashed border-[var(--color-border)] text-[var(--color-text-tertiary)] line-through',
                    draft.inherit ? 'cursor-default' : 'cursor-pointer',
                  ].join(' ')}
                >
                  {isDefault && (
                    <Star
                      className="h-3 w-3 fill-[var(--color-warning)] text-[var(--color-warning)]"
                      aria-label="Default"
                    />
                  )}
                  {choice.label}
                  {!choice.isActive && (
                    <span className="text-[10px] text-[var(--color-warning)]">retired</span>
                  )}
                  {delta !== null && delta !== 0 && (
                    <SignedAmount amount={delta} currency={group.currency} />
                  )}
                </button>
              );
            })}
            {activeChoices.length === 0 && (
              <span className="text-[11.5px] text-[var(--color-warning)]">
                Every choice in this group is retired — offering it shows the customer nothing.
              </span>
            )}
          </div>

          <label className="mt-1 flex items-center gap-2">
            <span className="text-[11px] text-[var(--color-text-tertiary)]">Default for this product</span>
            <select
              value={draft.defaultChoiceKey ?? ''}
              onChange={(e) => onChange({ defaultChoiceKey: e.target.value || null })}
              className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2 py-1 text-[12px] outline-none"
            >
              <option value="">Follow the group ({group.choices.find((c) => c.isRecommendedDefault)?.label ?? 'none'})</option>
              {/* OFFERED, not every active choice: a default the product does not offer is a
                  payload the backend rejects. */}
              {offered.map((choice) => (
                <option key={choice.key} value={choice.key}>
                  {choice.label}
                </option>
              ))}
            </select>
          </label>
        </div>
      )}
    </AonikCard>
  );
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
