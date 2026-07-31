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
//   2. A concurrent edit is REFUSED, not merged. `contentVersion` is re-read immediately
//      before the write and compared with the version this sheet loaded. Merging is what the
//      choice editor does for labels, and it would be wrong here: silently combining two
//      people's allergen edits produces a panel neither of them authored, and allergens are
//      the one field on this page where being wrong is a safety incident rather than a typo.

import { useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import { commerceContentService } from '@/services/commerceContentService';
import type { EffectiveOptionGroupDto, ProductContentDto } from '@/types/commerce';

import { ContentFields } from './ContentFields';
import { defaultSignature } from '../lib/offerSignature';
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
  groups,
  onClose,
  onSaved,
}: {
  productId: string;
  productName: string;
  /** Null when authoring the first block for this product. */
  block: ProductContentDto | null;
  /** The product's effective offer as the page last read it — see `openDefault`. */
  groups: EffectiveOptionGroupDto[];
  onClose: () => void;
  onSaved: () => void;
}) {
  // `baseline` is what this sheet is editing ON TOP OF. It starts as the block the page
  // passed in and is REPLACED by a conflict reload, so the version comparison below always
  // refers to what the operator can currently see.
  const [baseline, setBaseline] = useState<ProductContentDto | null>(block);
  /**
   * The default combination as it stood when this sheet opened — the preparation the operator
   * believes they are describing.
   *
   * `contentVersion` versions the block ROW, and the preparation it describes is not part of
   * that row: another operator changing the effective default moves what these figures will be
   * published against without touching anything the version comparison can see. When no block
   * exists yet there is no row to version at all, so the comparison passes unconditionally and
   * the first-authoring path — the one where every field is being written from scratch — was
   * the least guarded of the two.
   */
  const [openDefault] = useState(() => defaultSignature(groups));
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
      setConflict(false);
      onSaved();
    } catch (err: unknown) {
      setError(readMessage(err) || 'The latest content could not be read.');
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

    setSaving(true);
    setError(null);
    setConflict(false);
    try {
      // Re-read IMMEDIATELY before the write. The service reads the content row when the
      // request starts, so a payload built before someone else's committed edit still looks
      // current to it — the staleness has to be established here.
      //
      // Residual, stated rather than hidden: this is read-then-write, so an edit landing
      // inside the remaining window still wins. Closing that needs the upsert to accept the
      // contentVersion it was based on.
      // Fails CLOSED on an unreadable offer, for the same reason the variant sheet does: an
      // unread offer is not an unchanged one.
      let currentDefault: string;
      try {
        const product = await commerceCatalogService.getProduct(productId);
        currentDefault = defaultSignature(product.effectiveOptionGroups ?? []);
      } catch {
        setError(
          'The product’s current options could not be read, so this cannot be saved safely — ' +
            'the preparation these figures would describe cannot be confirmed. Try again.',
        );
        setSaving(false);
        return;
      }
      if (currentDefault !== openDefault) {
        setConflict(true);
        setError(
          'Someone else changed this product’s default combination while this was open, so ' +
            'these figures would be published against a different preparation than the one ' +
            'they were written for. Reload before saving.',
        );
        setSaving(false);
        return;
      }

      const fresh = await commerceContentService.getAdminContent(productId);
      if (baseline && fresh.block && fresh.block.contentVersion !== baseline.contentVersion) {
        setConflict(true);
        setError(
          'Someone else edited this block while it was open. Reload to see their version — ' +
            'saving now would replace every field, including any declarations they added.',
        );
        setSaving(false);
        return;
      }
      if (!baseline && fresh.block) {
        setConflict(true);
        setError('Someone else authored this block while this was open. Reload before saving.');
        setSaving(false);
        return;
      }

      await commerceContentService.upsertContent(productId, wireFromDraft(draft));
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
          <Button variant="outline" onClick={onClose} disabled={saving || reloading}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving || reloading || conflict}>
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
