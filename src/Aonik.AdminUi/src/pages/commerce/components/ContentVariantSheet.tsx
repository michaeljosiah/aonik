// Combination-variant authoring (Spec 075 §2).
//
// A variant is keyed by its complete canonical selection, and the SERVER does the
// canonicalisation (Spec 066) — this sheet sends a possibly-partial selection and lets the
// service normalise and store it complete. Canonicalising here would be a second implementation
// of a rule that decides variant identity, and a drift between the two would silently author a
// variant that matches nothing.
//
// Declarations left empty mean WITHHELD for this combination. Not inherited from the default
// block, not derived — the whole reason variants exist is that a salmon combination must never
// surface the standard preparation's shellfish line.

import { useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
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

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

interface ContentVariantSheetProps {
  productId: string;
  /** The product's effective offer — the only selections a variant can legitimately describe. */
  groups: EffectiveOptionGroupDto[];
  /** Editing an existing variant, or null when authoring a new one. */
  variant: ProductContentVariantDto | null;
  /** Pre-filled selection from a coverage gap, as canonical JSON. */
  initialSelectionJson?: string | null;
  onClose: () => void;
  onSaved: () => void;
}

export function ContentVariantSheet({
  productId,
  groups,
  variant,
  initialSelectionJson,
  onClose,
  onSaved,
}: ContentVariantSheetProps) {
  const [selection, setSelection] = useState<Record<string, string>>(() =>
    parseSelection(variant?.selectionJson ?? initialSelectionJson ?? null),
  );
  const [draft, setDraft] = useState<ContentDraft>(() =>
    variant ? draftFromVariant(variant) : emptyDraft(),
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    const invalid = validateDraft(draft);
    if (invalid) {
      setError(invalid);
      return;
    }
    const chosen = Object.entries(selection).filter(([, value]) => value !== '');
    if (chosen.length === 0) {
      setError('Pick at least one choice — a variant is identified by the combination it describes.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const payload = {
        // Possibly PARTIAL: the service normalises through Spec 066 and stores it complete.
        selectionJson: JSON.stringify(Object.fromEntries(chosen)),
        ...wireFromDraft(draft),
      };
      if (variant) await commerceContentService.updateVariant(variant.id, payload);
      else await commerceContentService.upsertVariant(productId, payload);
      toast.success(variant ? 'Variant saved' : 'Variant authored');
      onSaved();
      onClose();
    } catch (err: unknown) {
      // V-C2: a variant must publish every figure the default publishes. The message names the
      // missing figures, so it is shown verbatim rather than summarised.
      setError(readMessage(err) || 'The variant could not be saved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open onOpenChange={(open) => !open && !saving && onClose()}>
      <SheetContent size="md">
        <SheetHeader
          title={variant ? 'Edit combination' : 'Author a combination'}
          subtitle="Declarations left empty are withheld for this combination — never inherited"
        />

        <SheetBody>
          {error && (
            <p className="mb-3 flex items-start gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
              <AlertCircle className="mt-px h-4 w-4 shrink-0" aria-hidden />
              <span>{error}</span>
            </p>
          )}

          <fieldset disabled={saving} className="flex min-w-0 flex-col gap-4 border-0 p-0">
            <div>
              <p className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Combination
              </p>
              {groups.length === 0 ? (
                <p className="text-[12px] text-[var(--color-text-secondary)]">
                  This product offers no option groups, so it has no combinations to describe.
                </p>
              ) : (
                <div className="flex flex-col gap-2">
                  {groups.map((group) => (
                    <label key={group.key} className="flex items-center gap-2">
                      <span className="w-[130px] shrink-0 text-[12px] text-[var(--color-text-secondary)]">
                        {group.label ?? group.key}
                      </span>
                      <select
                        value={selection[group.key] ?? ''}
                        onChange={(e) =>
                          setSelection({ ...selection, [group.key]: e.target.value })
                        }
                        className={inputClass}
                        // Editing a variant cannot change what it identifies: the selection IS
                        // the key, so a change would silently retarget the authored content at
                        // a different combination.
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
                  ))}
                </div>
              )}
              {variant && (
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
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving}>
            {saving ? 'Saving…' : 'Save combination'}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

/** Canonical selection JSON to a form map. Malformed input yields an empty selection. */
function parseSelection(json: string | null): Record<string, string> {
  if (!json) return {};
  try {
    const parsed: unknown = JSON.parse(json);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};
    const out: Record<string, string> = {};
    for (const [key, value] of Object.entries(parsed as Record<string, unknown>)) {
      // Multi-select groups store arrays; this form authors one choice per group, so an array
      // is shown by its first member rather than crashing or silently dropping the group.
      if (typeof value === 'string') out[key] = value;
      else if (Array.isArray(value) && typeof value[0] === 'string') out[key] = value[0];
    }
    return out;
  } catch {
    return {};
  }
}

function readMessage(err: unknown): string {
  return err && typeof err === 'object' && 'userMessage' in err
    ? String((err as { userMessage?: string }).userMessage ?? '')
    : '';
}
