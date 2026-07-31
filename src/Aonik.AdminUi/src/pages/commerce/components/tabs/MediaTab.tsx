// Product editor — Media (Spec 082 §2). Full replace where ARRAY POSITION IS THE ORDER:
// the wire carries no sort field, so what you see top-to-bottom is exactly what the server
// stores. Nothing here invents a sortOrder, because a divergent one would be ignored and
// silently persist a different order than the screen shows.

import { ArrowDown, ArrowUp, Trash2 } from 'lucide-react';
import { useState } from 'react';

import { heroImageIndex, MEDIA_URL_MAX, moveItem } from '../../lib/productForm';
import { Field, inputClass } from './DetailsTab';

export interface MediaDraft {
  url: string;
  kind?: string | null;
}

interface MediaTabProps {
  items: MediaDraft[];
  onChange: (next: MediaDraft[]) => void;
}

export function MediaTab({ items, onChange }: MediaTabProps) {
  const [newUrl, setNewUrl] = useState('');
  const [addError, setAddError] = useState<string | null>(null);
  // The first IMAGE, not the first row — a leading document is not the hero.
  const heroIndex = heroImageIndex(items);

  const add = () => {
    const url = newUrl.trim();
    if (!url) return;
    // Caught at entry, not at save: the media write follows the details PATCH, so an
    // over-long URL rejected server-side would leave a half-saved product behind.
    if (url.length > MEDIA_URL_MAX) {
      setAddError(`A media URL is at most ${MEDIA_URL_MAX} characters — this one is ${url.length}.`);
      return;
    }
    setAddError(null);
    onChange([...items, { url, kind: 'image' }]);
    setNewUrl('');
  };

  return (
    <div className="flex flex-col gap-4">
      <p className="text-[11px] text-[var(--color-text-tertiary)]">
        Order is position — the first image is the hero. Saving replaces the whole list.
        {heroIndex === -1 && items.length > 0 && ' No image here, so the storefront has no hero.'}
      </p>

      {items.length === 0 ? (
        <p className="rounded-md border border-dashed border-[var(--color-border)] py-6 text-center text-sm text-[var(--color-text-secondary)]">
          No media yet.
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item, index) => (
            <li
              key={`${item.url}-${index}`}
              className="flex items-center gap-2.5 rounded-md border border-[var(--color-border-light)] p-2"
            >
              <img
                src={item.url}
                alt=""
                className="h-10 w-10 rounded object-cover"
                // A broken or unreachable URL must not leave a broken-image glyph in the
                // editor; the row still shows its URL so it can be fixed or removed.
                onError={(e) => {
                  e.currentTarget.style.visibility = 'hidden';
                }}
              />
              <span className="min-w-0 flex-1 truncate font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                {item.url}
              </span>
              {index === heroIndex && (
                <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 text-[10px] font-semibold text-[var(--color-text-secondary)]">
                  Hero
                </span>
              )}
              <button
                type="button"
                aria-label="Move up"
                disabled={index === 0}
                onClick={() => onChange(moveItem(items, index, index - 1))}
                className="rounded p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] disabled:opacity-30"
              >
                <ArrowUp className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                aria-label="Move down"
                disabled={index === items.length - 1}
                onClick={() => onChange(moveItem(items, index, index + 1))}
                className="rounded p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] disabled:opacity-30"
              >
                <ArrowDown className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                aria-label="Remove"
                onClick={() => onChange(items.filter((_, i) => i !== index))}
                className="rounded p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-error)]"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <Field label="Add image by URL">
        <div className="flex gap-2">
          <input
            value={newUrl}
            onChange={(e) => setNewUrl(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                add();
              }
            }}
            placeholder="https://…"
            className={inputClass}
          />
          <button
            type="button"
            onClick={add}
            className="rounded-md border border-[var(--color-border)] px-3 text-[13px] text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)]"
          >
            Add
          </button>
        </div>
        {addError && <p className="mt-1 text-[11px] text-[var(--color-error)]">{addError}</p>}
      </Field>
    </div>
  );
}
