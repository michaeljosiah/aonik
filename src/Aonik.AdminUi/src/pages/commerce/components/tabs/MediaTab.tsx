// Product editor — Media (Spec 082 §2). Full replace where ARRAY POSITION IS THE ORDER:
// the wire carries no sort field, so what you see top-to-bottom is exactly what the server
// stores. Nothing here invents a sortOrder, because a divergent one would be ignored and
// silently persist a different order than the screen shows.

import { ArrowDown, ArrowUp, Trash2 } from 'lucide-react';
import { useRef, useState } from 'react';

import { heroImageIndex, MEDIA_ALT_MAX, MEDIA_URL_MAX, moveItem, validateImageUpload } from '../../lib/productForm';
import { Field, inputClass } from './DetailsTab';
import { Badge } from '@/components/ui/badge';

export interface MediaDraft {
  url: string;
  kind?: string | null;
  altText?: string | null;
}

interface MediaTabProps {
  items: MediaDraft[];
  onChange: (next: MediaDraft[]) => void;
  onUpload: (file: File, altText: string) => Promise<void>;
  uploading: boolean;
}

export function MediaTab({ items, onChange, onUpload, uploading }: MediaTabProps) {
  const [newUrl, setNewUrl] = useState('');
  const [newAltText, setNewAltText] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);
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
    onChange([...items, { url, kind: 'image', altText: newAltText.trim() || null }]);
    setNewUrl('');
    setNewAltText('');
  };

  const upload = async () => {
    if (!file) return;
    const error = validateImageUpload(file, newAltText);
    if (error) { setAddError(error); return; }
    setAddError(null);
    try {
      await onUpload(file, newAltText.trim());
      setFile(null);
      setNewAltText('');
      if (fileInput.current) fileInput.current.value = '';
    } catch (error: unknown) {
      setAddError(error && typeof error === 'object' && 'userMessage' in error
        ? String(error.userMessage) : 'The image could not be uploaded. Your gallery changes are preserved.');
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <p className="text-[11px] text-muted-foreground">
        Order is position — the first image is the hero. Saving replaces the whole list.
        {heroIndex === -1 && items.length > 0 && ' No image here, so the storefront has no hero.'}
      </p>

      {items.length === 0 ? (
        <p className="rounded-md border border-dashed border-border py-6 text-center text-sm text-muted-foreground">
          No media yet.
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item, index) => (
            <li
              key={`${item.url}-${index}`}
              className="flex items-center gap-2.5 rounded-md border border-border p-2"
            >
              <img
                src={item.url}
                alt={item.altText ?? ''}
                className="h-10 w-10 rounded object-cover"
                // A broken or unreachable URL must not leave a broken-image glyph in the
                // editor; the row still shows its URL so it can be fixed or removed.
                onError={(e) => {
                  e.currentTarget.style.visibility = 'hidden';
                }}
              />
              <div className="min-w-0 flex-1">
                <p className="truncate font-[family-name:var(--font-mono)] text-[11px] text-muted-foreground">{item.url}</p>
                <Field label="Alt text">
                  <input value={item.altText ?? ''} maxLength={MEDIA_ALT_MAX} className={inputClass}
                    onChange={(event) => onChange(items.map((row, rowIndex) => rowIndex === index
                      ? { ...row, altText: event.target.value } : row))} />
                </Field>
              </div>
              {index === heroIndex && (
                <Badge variant="secondary">Hero</Badge>
              )}
              <button
                type="button"
                aria-label="Move up"
                disabled={index === 0}
                onClick={() => onChange(moveItem(items, index, index - 1))}
                className="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-30"
              >
                <ArrowUp className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                aria-label="Move down"
                disabled={index === items.length - 1}
                onClick={() => onChange(moveItem(items, index, index + 1))}
                className="rounded p-1 text-muted-foreground hover:text-foreground disabled:opacity-30"
              >
                <ArrowDown className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                aria-label="Remove"
                onClick={() => onChange(items.filter((_, i) => i !== index))}
                className="rounded p-1 text-muted-foreground hover:text-destructive"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <Field label="New image alt text">
        <input value={newAltText} maxLength={MEDIA_ALT_MAX} className={inputClass}
          onChange={(event) => setNewAltText(event.target.value)} />
        <span className="text-[11px] text-muted-foreground">Describe the dish shown. Required for uploads.</span>
      </Field>

      <Field label="Upload product image">
        <input ref={fileInput} type="file" accept="image/jpeg,image/png" className={inputClass}
          onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
        <button type="button" disabled={!file || uploading} onClick={() => void upload()}
          className="rounded-md border border-border px-3 py-1.5 text-[13px] hover:bg-muted disabled:opacity-50">
          {uploading ? 'Uploading…' : 'Upload image'}
        </button>
        <span className="text-[11px] text-muted-foreground">JPEG or PNG, up to 10 MiB. Save the product to publish the updated gallery.</span>
      </Field>

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
            className="rounded-md border border-border px-3 text-[13px] text-foreground hover:bg-muted"
          >
            Add
          </button>
        </div>
        {addError && <p className="mt-1 text-[11px] text-destructive">{addError}</p>}
      </Field>
    </div>
  );
}
