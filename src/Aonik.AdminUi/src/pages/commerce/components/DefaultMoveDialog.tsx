// Recommended-default move (Spec 074 §2) — a CONSEQUENCE surface, not a confirmation.
//
// Moving a group's recommended default changes the standard preparation of every product that
// inherits it, which flags those products' content blocks for review (Spec 067). The dialog
// exists to show that blast radius, so it does not close on success: it stays open and renders
// the affected products the API actually reported, with a way to go and deal with them.
//
// The copy never says "all products offering this group". Products that pin their own
// defaultChoiceKey are unaffected BY DEFINITION and are absent from the report — claiming
// otherwise would send the operator hunting for products that were never touched.

import { useState } from 'react';
import { ArrowRight, ExternalLink } from 'lucide-react';
import { Link } from 'react-router-dom';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Pill } from '@/components/layout/aonik';
import { commerceCatalogService } from '@/services/commerceCatalogService';
import type { OptionChoiceDto, OptionGroupDto, RecommendedDefaultChangeResult } from '@/types/commerce';

interface DefaultMoveDialogProps {
  group: OptionGroupDto;
  /** The choice being promoted. */
  target: OptionChoiceDto;
  /** The choice losing the default, when the group has one. */
  current: OptionChoiceDto | null;
  onClose: () => void;
  /** Called once the move succeeded, so the page can reload — NOT a close. */
  onMoved: () => void;
}

export function DefaultMoveDialog({
  group,
  target,
  current,
  onClose,
  onMoved,
}: DefaultMoveDialogProps) {
  const [result, setResult] = useState<RecommendedDefaultChangeResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const move = async () => {
    setSaving(true);
    setError(null);
    try {
      setResult(await commerceCatalogService.setRecommendedDefault(group.id, target.key));
      onMoved();
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'The default could not be moved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    // Escape and outside-click route through here too, so a dismissal mid-flight would
    // unmount the one component that reports the blast radius — the move would land and the
    // operator would never see which products it changed. That is the whole point of the
    // dialog, so it refuses to close until the request settles.
    <Dialog open onOpenChange={(open) => !open && !saving && onClose()}>
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>{result ? 'Default moved' : 'Move the recommended default?'}</DialogTitle>
          <DialogDescription>
            {result
              ? `${group.label} now recommends ${target.label}.`
              : 'This changes the standard preparation for every product that inherits this group’s default.'}
          </DialogDescription>
        </DialogHeader>

        {!result && (
          <div className="flex items-center gap-2.5 rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2.5">
            <span className="text-[13px] text-[var(--color-text-secondary)]">
              {current ? current.label : 'No default'}
            </span>
            <ArrowRight className="h-4 w-4 shrink-0 text-[var(--color-text-tertiary)]" aria-hidden />
            <span className="text-[13px] font-semibold text-[var(--color-text-primary)]">
              {target.label}
            </span>
          </div>
        )}

        {error && (
          <p className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12px] text-[var(--color-error)]">
            {error}
          </p>
        )}

        {result && <AffectedProducts slugs={result.affectedProductSlugs} />}

        <DialogFooter>
          {result ? (
            <>
              {result.affectedProductSlugs.length > 0 && (
                <Button variant="outline" asChild>
                  <Link to="/commerce/content">
                    Review content
                    <ExternalLink className="ml-1.5 h-3.5 w-3.5" aria-hidden />
                  </Link>
                </Button>
              )}
              <Button onClick={onClose}>Done</Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={onClose} disabled={saving}>
                Cancel
              </Button>
              <Button onClick={() => void move()} disabled={saving}>
                {saving ? 'Moving…' : 'Move default'}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function AffectedProducts({ slugs }: { slugs: string[] }) {
  if (slugs.length === 0) {
    return (
      <p className="text-[12.5px] text-[var(--color-text-secondary)]">
        No product’s standard preparation changed. Products that pin their own default for this
        group are unaffected by design, so they are not listed here.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <p className="text-[12.5px] text-[var(--color-text-secondary)]">
        {slugs.length} product{slugs.length === 1 ? '' : 's'} inherited this default, so{' '}
        {slugs.length === 1 ? 'its' : 'their'} standard preparation just changed and{' '}
        {slugs.length === 1 ? 'its content block is' : 'their content blocks are'} now flagged for
        review. Declarations stay withheld until confirmed.
      </p>
      <div className="flex max-h-[180px] flex-wrap gap-1.5 overflow-y-auto">
        {slugs.map((slug) => (
          <Pill key={slug} tone="warning" size="sm">
            {slug}
          </Pill>
        ))}
      </div>
    </div>
  );
}
