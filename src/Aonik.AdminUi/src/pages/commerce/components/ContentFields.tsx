// The field set shared by the default block and a combination variant (Spec 075 §3). One
// component because the two write the SAME seven figures, two declarations and heating list —
// and the recurring defect across this series has been a rule that reached some of its call
// sites, so the fields and their validation live in one place rather than two.

import { Plus, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { FIGURE_FIELDS, type FigureKey, type HeatingStep } from '../lib/contentState';
import type { ContentDraft } from '../lib/contentDraft';

const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

export function ContentFields({
  draft,
  onChange,
}: {
  draft: ContentDraft;
  onChange: (next: ContentDraft) => void;
}) {
  const setFigure = (key: FigureKey, value: string) =>
    onChange({ ...draft, figures: { ...draft.figures, [key]: value } });

  // Shown only for legacy damage: the stored JSON does not parse, so the resolver withholds
  // heating entirely. The column is required, so "withheld" is not a state this form can write
  // back — whatever is saved becomes a claim. That makes replacing it a decision rather than a
  // consequence of editing a figure, which is what it silently was.
  // Matched to what validateDraft actually requires: a COMPLETED step. Keying the panel on row
  // count would hide the acknowledgement the moment an empty row is added, while the save still
  // demands it — a form asking for something it no longer shows.
  const unreadableHeating =
    draft.heatingUnreadable &&
    draft.heating.filter((s) => s.method.trim() !== '' && s.body.trim() !== '').length === 0;

  const setStep = (index: number, patch: Partial<HeatingStep>) =>
    onChange({
      ...draft,
      heating: draft.heating.map((step, i) => (i === index ? { ...step, ...patch } : step)),
    });

  return (
    <div className="flex flex-col gap-4">
      <label className="flex flex-col gap-1">
        <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Serving label
        </span>
        <input
          value={draft.servingLabel}
          onChange={(e) => onChange({ ...draft, servingLabel: e.target.value })}
          placeholder="e.g. Per 350 g serving"
          className={inputClass}
        />
      </label>

      <div>
        <p className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Figures
        </p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {FIGURE_FIELDS.map((field) => (
            <label key={field.key} className="flex flex-col gap-1">
              <span className="text-[11px] text-[var(--color-text-secondary)]">
                {field.label} <span className="text-[var(--color-text-tertiary)]">({field.unit})</span>
              </span>
              <input
                value={draft.figures[field.key as FigureKey]}
                onChange={(e) => setFigure(field.key as FigureKey, e.target.value)}
                inputMode="decimal"
                placeholder="—"
                className={`${inputClass} font-[family-name:var(--font-mono)]`}
              />
            </label>
          ))}
        </div>
        <p className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">
          {/* The single most important sentence on this form. */}
          An EMPTY box means not published. It is not the same as 0, which is a published claim
          about the food.
        </p>
      </div>

      <label className="flex flex-col gap-1">
        <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Ingredients
        </span>
        <textarea
          value={draft.ingredients}
          onChange={(e) => onChange({ ...draft, ingredients: e.target.value })}
          rows={3}
          className={inputClass}
        />
      </label>

      <label className="flex flex-col gap-1">
        <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Allergens
        </span>
        <textarea
          value={draft.allergens}
          onChange={(e) => onChange({ ...draft, allergens: e.target.value })}
          rows={2}
          className={inputClass}
        />
        <span className="text-[11px] text-[var(--color-text-tertiary)]">
          Left empty, this is withheld from customers — never substituted from anywhere else.
        </span>
      </label>

      <div>
        <p className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Heating &amp; usage
        </p>
        <div className="flex flex-col gap-2">
          {draft.heating.map((step, index) => (
            <div key={index} className="flex gap-2">
              <input
                value={step.method}
                onChange={(e) => setStep(index, { method: e.target.value })}
                placeholder="Method"
                className={`${inputClass} w-[140px]`}
              />
              <input
                value={step.body}
                onChange={(e) => setStep(index, { body: e.target.value })}
                placeholder="Instruction"
                className={inputClass}
              />
              <button
                type="button"
                aria-label="Remove step"
                onClick={() =>
                  onChange({ ...draft, heating: draft.heating.filter((_, i) => i !== index) })
                }
                className="rounded p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-error)]"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
          <Button
            variant="outline"
            size="sm"
            onClick={() => onChange({ ...draft, heating: [...draft.heating, { method: '', body: '' }] })}
          >
            <Plus className="mr-1 h-3.5 w-3.5" /> Add a step
          </Button>
          {unreadableHeating && (
            <div className="rounded border border-[var(--color-warning)] bg-[var(--color-warning-light)] p-2.5">
              <p className="text-[11px] text-[var(--color-text-secondary)]">
                The stored heating steps for this product cannot be read, so customers are shown
                nothing for them. They cannot be kept that way — whatever is saved here becomes a
                claim.
              </p>
              <label className="mt-2 flex items-start gap-2 text-[11px] text-[var(--color-text-secondary)]">
                <input
                  type="checkbox"
                  checked={draft.heatingReplacementAccepted}
                  onChange={(e) =>
                    onChange({ ...draft, heatingReplacementAccepted: e.target.checked })
                  }
                  className="mt-0.5"
                />
                <span>
                  Publish an explicit “no heating required” panel. Leave this unticked and add the
                  steps instead if the product needs any.
                </span>
              </label>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
