// Default-block authoring (Spec 075 §1).
//
// The upsert is a FULL REPLACE: `UpsertContentAsync` assigns all eleven members
// unconditionally (ProductContentService.cs:190-201), so a field this form does not carry is a
// field deleted. Two consequences shape the whole component:
//
//   1. The form always seeds from the RAW admin read, never from the public resolution — the
//      resolution withholds declarations and can resolve to a variant, so saving from it would
//      overwrite text nobody ever saw.
//
//   2. A concurrent edit is REFUSED, not merged. The block's AUTHORED FIELDS are re-read
//      immediately before the write and compared with what this sheet loaded — not
//      `contentVersion`, which the shared write pipeline bumps for variant writes too, so an
//      unrelated variant edit would have discarded the operator's draft. Merging is what the
//      choice editor does for labels, and it would be wrong here: silently combining two
//      people's allergen edits produces a panel neither of them authored, and allergens are
//      the one field on this page where being wrong is a safety incident rather than a typo.

import { useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceContentService } from '@/services/commerceContentService';
import type { ProductContentDto } from '@/types/commerce';

import { ContentFields } from './ContentFields';
import {
  draftFromBlock,
  validateDraft,
  wireFromDraft,
  type ContentDraft,
} from '../lib/contentDraft';

export function ContentBlockSheet({
  productId,
  productName,
  block,
  expectedDefaults,
  isStale,
  onClose,
  onSaved,
}: {
  productId: string;
  productName: string;
  /** Null when authoring the first block for this product. */
  block: ProductContentDto | null;
  /** The standard preparation as of the read this sheet opened from (V-C9). */
  expectedDefaults: string;
  /** The block no longer describes the current standard preparation. */
  isStale: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  // `baseline` is what this sheet is editing ON TOP OF. It starts as the block the page
  // passed in and is REPLACED by a conflict reload, so the version comparison below always
  // refers to what the operator can currently see.
  const [baseline, setBaseline] = useState<ProductContentDto | null>(block);
  /**
   * The standard preparation this draft is being written against.
   *
   * Sent with the write and enforced THERE (V-C9). It is the server's own canonical binding,
   * from the same read that produced the block — not a signature this client derives from an
   * offer it fetches separately, which needed a second read, could fail on its own, and
   * compared a value the server never sees. A conflict reload replaces it, because the operator
   * is then looking at the newer preparation.
   */
  const [reviewedDefaults, setReviewedDefaults] = useState(expectedDefaults);
  const [confirming, setConfirming] = useState(false);
  const [draft, setDraft] = useState<ContentDraft>(() => draftFromBlock(block));
  const [saving, setSaving] = useState(false);
  const [reloading, setReloading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  // The sheet stays mounted under the same key after a conflict, so nothing re-initialises on
  // its own: draft, baseline and the conflict flag all have to be reset here or Save stays
  // disabled over stale state and the operator has to discover that closing and reopening is
  // the real reload.
  const reload = async () => {
    setReloading(true);
    setError(null);
    try {
      const fresh = await commerceContentService.getAdminContent(productId);
      setBaseline(fresh.block);
      setDraft(draftFromBlock(fresh.block));
      setReviewedDefaults(fresh.currentDefaultsSelectionJson);
      setConflict(false);
      onSaved();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The latest content could not be read.');
    } finally {
      setReloading(false);
    }
  };

  /** "Reviewed, still correct" — available only with the block's own text in view. */
  const confirmNoChanges = async () => {
    setConfirming(true);
    setError(null);
    try {
      await commerceContentService.confirmReview(productId, reviewedDefaults);
      toast.success('Review confirmed');
      onSaved();
      onClose();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The review could not be confirmed.');
    } finally {
      setConfirming(false);
    }
  };

  const save = async () => {
    const invalid = validateDraft(draft);
    if (invalid) {
      setError(invalid);
      return;
    }

    setSaving(true);
    setError(null);
    setConflict(false);
    try {
      // Re-read IMMEDIATELY before the write. The service reads the content row when the
      // request starts, so a payload built before someone else's committed edit still looks
      // current to it — the staleness has to be established here. Compared on the authored
      // fields, because the version column moves for writes to other rows entirely.
      //
      // Residual, stated rather than hidden: this is read-then-write, so an edit landing
      // inside the remaining window still wins. Closing that needs the upsert to accept the
      // contentVersion it was based on.
      // A COURTESY check, not the guard. It turns the common race into a clear message without
      // a round trip, but the write below carries both preconditions and the service enforces
      // them inside its serialized attempt — which is the only place a racing writer cannot get
      // in front of. Everything this compares was read before the request; the window between
      // that read and the write is exactly what the server-side check closes.
      const fresh = await commerceContentService.getAdminContent(productId);
      if ((fresh.block?.blockSignature ?? null) !== (baseline?.blockSignature ?? null)) {
        setConflict(true);
        setError(
          baseline
            ? 'Someone else edited this block while it was open. Reload to see their version — ' +
              'saving now would replace every field, including any declarations they added.'
            : 'Someone else authored this block while this was open. Reload before saving.',
        );
        setSaving(false);
        return;
      }

      await commerceContentService.upsertContent(productId, {
        ...wireFromDraft(draft),
        expectedDefaultsSelectionJson: reviewedDefaults,
        expectedBlockSignature: baseline?.blockSignature ?? null,
      });
      toast.success('Content saved');
      onSaved();
      onClose();
    } catch (err: unknown) {
      // Cross-row invariants (V-C6: the block cannot publish a figure an active variant does
      // not) come back as validation errors naming the offending variants. Shown verbatim —
      // paraphrasing a rule this page does not own would send the operator to the wrong place.
      setError(readMessage(err) || 'The content could not be saved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Sheet open onOpenChange={(open) => !open && !saving && onClose()}>
      <SheetContent size="md">
        <SheetHeader
          title={baseline ? 'Edit the default block' : 'Author the default block'}
          subtitle={productName}
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
            <ContentFields draft={draft} onChange={setDraft} />
          </fieldset>
        </SheetBody>

        <SheetFooter>
          <span className="mr-auto max-w-[280px] text-[11px] text-[var(--color-text-tertiary)]">
            Saving replaces every field of this block, and clears its review flag.
          </span>
          {/*
            Confirming lives HERE, not on the queue row, because this is the only place the
            declarations being confirmed are on screen. The workbench prefers the resolved
            panel, which withholds ingredients and allergens precisely while a block is stale —
            so a confirm button beside it published unseen stored text, and the operator could
            not have inspected it even if they wanted to.
          */}
          {isStale && baseline && (
            <Button
              variant="outline"
              onClick={() => void confirmNoChanges()}
              disabled={saving || reloading || confirming || conflict}
            >
              {confirming ? 'Confirming…' : 'Confirm — no changes needed'}
            </Button>
          )}
          <Button variant="outline" onClick={onClose} disabled={saving || reloading}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving || reloading || confirming || conflict}>
            {saving ? 'Saving…' : 'Save block'}
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
