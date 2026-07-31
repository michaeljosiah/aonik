// The content workbench (Spec 075 §2) — the customer-facing label, rendered as the customer
// actually reads it rather than as the database stores it.
//
// That distinction is the point of the card. Under review the resolver WITHHOLDS ingredients,
// allergens and heating, so this shows them withheld even though the text is sitting right
// there in the block. An admin screen that displays authored-but-withheld text as though it
// were live tells the operator their allergen line is serving customers when it is not — which
// is the one mistake a content tool must not make.

import { AlertTriangle, FilePlus } from 'lucide-react';

import { Card as AonikCard, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import type { HeatingStepDto, ProductContentDto } from '@/types/commerce';

import {
  FIGURE_FIELDS,
  renderDeclaration,
  type ContentState,
  type FigureKey,
} from '../lib/contentState';

interface ContentWorkbenchProps {
  block: ProductContentDto | null;
  state: ContentState;
  /** Absent in read-only embeddings (Spec 082's product editor preview). */
  onAuthor?: () => void;
  onEdit?: () => void;
}

export function ContentWorkbench({ block, state, onAuthor, onEdit }: ContentWorkbenchProps) {
  if (state === 'none' || !block) {
    return (
      <AonikCard padding={0}>
        <div className="m-3 flex flex-col items-center gap-2 rounded-md border border-dashed border-[var(--color-border)] px-4 py-10 text-center">
          <p className="text-[13px] text-[var(--color-text-primary)]">Nothing published</p>
          <p className="max-w-[380px] text-[12px] text-[var(--color-text-secondary)]">
            The product page shows its explicit not-yet-published state. No figures, no
            declarations, nothing withheld — there is simply no block.
          </p>
          {onAuthor && (
            <Button variant="outline" size="sm" onClick={onAuthor}>
              <FilePlus className="mr-1 h-3.5 w-3.5" /> Author the default block
            </Button>
          )}
        </div>
      </AonikCard>
    );
  }

  return (
    <AonikCard
      title="Standard preparation"
      subtitle={block.servingLabel}
      padding={0}
      action={
        onEdit && (
          <Button variant="outline" size="sm" onClick={onEdit}>
            Edit block
          </Button>
        )
      }
    >
      {state === 'review' && (
        <p className="mx-3 mt-3 flex items-start gap-2 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-3 py-2 text-[12px] text-[var(--color-warning)]">
          <AlertTriangle className="mt-px h-4 w-4 shrink-0" aria-hidden />
          <span>
            The standard preparation changed underneath this block. Until it is confirmed,
            customers see the figures <strong>captioned as the standard preparation</strong> and
            the declarations <strong>withheld</strong> — ingredients, allergens and heating alike.
          </span>
        </p>
      )}

      <div className="p-3">
        <FigureGrid nutrition={block.nutrition} />

        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <DeclarationCell label="Ingredients" text={block.ingredients} state={state} />
          <DeclarationCell label="Allergens" text={block.allergens} state={state} />
        </div>

        <Heating steps={block.heating} state={state} />
      </div>
    </AonikCard>
  );
}

function FigureGrid({ nutrition }: { nutrition: ProductContentDto['nutrition'] }) {
  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-1.5 sm:grid-cols-4 lg:grid-cols-7">
      {FIGURE_FIELDS.map((field) => {
        const value = nutrition[field.key as FigureKey];
        return (
          <div key={field.key} className="flex flex-col">
            <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
              {field.label}
            </span>
            <span className="font-[family-name:var(--font-mono)] text-[13px] tabular-nums text-[var(--color-text-primary)]">
              {/* A DASH for null, never 0. "0 g salt" is a published claim about the food;
                  nothing published is the absence of one. */}
              {value == null ? '—' : `${value}${field.unit === 'kcal' ? '' : ''}`}
              {value != null && (
                <span className="ml-0.5 text-[10px] text-[var(--color-text-tertiary)]">
                  {field.unit}
                </span>
              )}
            </span>
          </div>
        );
      })}
    </div>
  );
}

function DeclarationCell({
  label,
  text,
  state,
}: {
  label: string;
  text: string | null;
  state: ContentState;
}) {
  const render = renderDeclaration(text, state);
  return (
    <div className="rounded-md border border-[var(--color-border-light)] p-2.5">
      <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </p>
      {render.kind === 'authored' && (
        <p className="text-[12.5px] text-[var(--color-text-primary)]">{render.text}</p>
      )}
      {render.kind === 'withheld-review' && (
        <p className="text-[12.5px] italic text-[var(--color-warning)]">Withheld while under review</p>
      )}
      {render.kind === 'absent' && (
        <p className="text-[12.5px] italic text-[var(--color-text-tertiary)]">
          Not yet published — exact-authored or absent, never substituted
        </p>
      )}
    </div>
  );
}

function Heating({ steps, state }: { steps: HeatingStepDto[] | null; state: ContentState }) {
  const hasSteps = !!steps && steps.length > 0;

  return (
    <div className="mt-3 rounded-md border border-[var(--color-border-light)] p-2.5">
      <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        Heating &amp; usage
      </p>
      {state === 'review' ? (
        // Heating withholds exactly like the other declarations under review — it is a usage
        // instruction, not a figure, so nothing about it may fall back.
        <p className="text-[12.5px] italic text-[var(--color-warning)]">Withheld while under review</p>
      ) : hasSteps ? (
        <ul className="flex flex-col gap-1">
          {steps!.map((step, index) => (
            <li key={`${step.method}-${index}`} className="flex gap-2">
              <Pill tone="muted" size="sm">
                {step.method}
              </Pill>
              <span className="text-[12.5px] text-[var(--color-text-primary)]">{step.body}</span>
            </li>
          ))}
        </ul>
      ) : (
        // A BLOCK with no steps is an authored EMPTY panel, not a withheld one: the upsert
        // coerces a null heatingJson to "[]", so the resolver reports heating as not withheld
        // and the customer receives an explicitly empty panel. Calling that "not yet
        // published" would tell the operator a gap is being flagged when it is not.
        <p className="text-[12.5px] italic text-[var(--color-text-tertiary)]">
          No steps published — customers see an explicitly empty panel, not a withheld one
        </p>
      )}
    </div>
  );
}
