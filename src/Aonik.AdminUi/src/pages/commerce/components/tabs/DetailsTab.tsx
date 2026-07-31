// Product editor — Details (Spec 082 §2). Slug is display-only after create: it is the
// stable handle collections, content bindings and storefront links resolve against.

import { Pill } from '@/components/layout/aonik';
import type { ProductCategoryDto } from '@/types/commerce';

import { validateAttributesJson, type ProductEditorForm } from '../../lib/productForm';

const STATUSES = ['Active', 'Draft', 'Archived'];

interface DetailsTabProps {
  slug: string;
  kind: string;
  form: ProductEditorForm;
  categories: ProductCategoryDto[];
  onChange: (patch: Partial<ProductEditorForm>) => void;
}

export function DetailsTab({ slug, kind, form, categories, onChange }: DetailsTabProps) {
  const attributesError = validateAttributesJson(form.attributesJson);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-secondary)]">
          {slug}
        </span>
        <Pill tone="muted" size="sm">
          {kind}
        </Pill>
        <span className="text-[11px] text-[var(--color-text-tertiary)]">
          Slug is fixed after create — links and content bindings resolve against it
        </span>
      </div>

      <Field label="Name">
        <input
          value={form.name}
          onChange={(e) => onChange({ name: e.target.value })}
          className={inputClass}
        />
      </Field>

      <Field label="Description">
        <textarea
          value={form.description}
          onChange={(e) => onChange({ description: e.target.value })}
          rows={3}
          className={inputClass}
        />
      </Field>

      <div className="flex gap-3">
        <Field label="Status" className="flex-1">
          <select
            value={form.status}
            onChange={(e) => onChange({ status: e.target.value })}
            className={inputClass}
          >
            {/* A status the server holds but this list does not know still renders, so an
                editor never silently rewrites it by saving. */}
            {!STATUSES.includes(form.status) && form.status && (
              <option value={form.status}>{form.status}</option>
            )}
            {STATUSES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Category" className="flex-1">
          <select
            value={form.categoryId ?? ''}
            onChange={(e) => onChange({ categoryId: e.target.value || null })}
            className={inputClass}
          >
            <option value="">Uncategorised</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
                {category.isActive ? '' : ' (retired)'}
              </option>
            ))}
          </select>
        </Field>
      </div>

      <Field label="Tags">
        <ChipEditor
          values={form.tags}
          placeholder="Add a tag"
          onChange={(tags) => onChange({ tags })}
        />
      </Field>

      <Field label="Attributes JSON">
        <textarea
          value={form.attributesJson}
          onChange={(e) => onChange({ attributesJson: e.target.value })}
          rows={5}
          spellCheck={false}
          className={`${inputClass} font-[family-name:var(--font-mono)] text-[12px]`}
        />
        <p className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">
          The attribute contract facet groups match on — paths traverse from this JSON's root.
        </p>
        {attributesError && (
          <p className="mt-1 text-[11px] text-[var(--color-error)]">{attributesError}</p>
        )}
      </Field>
    </div>
  );
}

// ─── Small shared editor pieces ────────────────────────────────────────────

export const inputClass =
  'w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-[13px] text-[var(--color-text-primary)] outline-none focus:border-[var(--color-brand-primary)]';

export function Field({
  label,
  children,
  className,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <label className={`flex flex-col gap-1 ${className ?? ''}`}>
      <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </span>
      {children}
    </label>
  );
}

export function ChipEditor({
  values,
  placeholder,
  onChange,
}: {
  values: string[];
  placeholder: string;
  onChange: (next: string[]) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] p-1.5">
      {values.map((value, index) => (
        <span
          key={`${value}-${index}`}
          className="flex items-center gap-1 rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 text-[11.5px] text-[var(--color-text-primary)]"
        >
          {value}
          <button
            type="button"
            aria-label={`Remove ${value}`}
            onClick={() => onChange(values.filter((_, i) => i !== index))}
            className="text-[var(--color-text-tertiary)] hover:text-[var(--color-error)]"
          >
            ×
          </button>
        </span>
      ))}
      <input
        placeholder={placeholder}
        className="min-w-[120px] flex-1 bg-transparent px-1 py-0.5 text-[13px] outline-none"
        onKeyDown={(e) => {
          if (e.key !== 'Enter') return;
          e.preventDefault();
          const value = e.currentTarget.value.trim();
          // Duplicates are dropped rather than stored twice — the server replaces the whole
          // list, so a duplicate would persist.
          if (value && !values.includes(value)) onChange([...values, value]);
          e.currentTarget.value = '';
        }}
      />
    </div>
  );
}
