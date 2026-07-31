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
  onClose,
  onSaved,
}: {
  productId: string;
  productName: string;
  /** Null when authoring the first block for this product. */
  block: ProductContentDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [draft, setDraft] = useState<ContentDraft>(() => draftFromBlock(block));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

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
      const fresh = await commerceContentService.getAdminContent(productId);
      if (block && fresh.block && fresh.block.contentVersion !== block.contentVersion) {
        setConflict(true);
        setError(
          'Someone else edited this block while it was open. Reload to see their version — ' +
            'saving now would replace every field, including any declarations they added.',
        );
        setSaving(false);
        return;
      }
      if (!block && fresh.block) {
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
          title={block ? 'Edit the default block' : 'Author the default block'}
          subtitle={productName}
        />

        <SheetBody>
          {error && (
            <div className="mb-3 flex items-start gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
              <AlertCircle className="mt-px h-4 w-4 shrink-0" aria-hidden />
              <span className="flex-1">{error}</span>
              {conflict && (
                <button type="button" onClick={onSaved} className="shrink-0 underline">
                  Reload
                </button>
              )}
            </div>
          )}

          <fieldset disabled={saving} className="flex min-w-0 flex-col gap-4 border-0 p-0">
            <ContentFields draft={draft} onChange={setDraft} />
          </fieldset>
        </SheetBody>

        <SheetFooter>
          <span className="mr-auto max-w-[280px] text-[11px] text-[var(--color-text-tertiary)]">
            Saving replaces every field of this block, and clears its review flag.
          </span>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={() => void save()} disabled={saving || conflict}>
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
