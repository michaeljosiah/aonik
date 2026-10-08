// Product editor — Details (Spec 082 §2). Slug is display-only after create: it is the
// stable handle collections, content bindings and storefront links resolve against.

import { Pill } from '@/components/layout/aonik';
import type { AdminCollectionSummaryDto, FacetOptionDto, ProductCategoryDto } from '@/types/commerce';

import { validateAttributesJson, type ProductEditorForm } from '../../lib/productForm';
import { NativeSelect } from '@/components/ui/native-select';
import { Checkbox } from '@/components/ui/checkbox';

const STATUSES = ['Active', 'Draft', 'Archived'];

interface DetailsTabProps {
  slug: string;
  kind: string;
  form: ProductEditorForm;
  categories: ProductCategoryDto[];
  tagOptions: FacetOptionDto[] | null;
  collections: AdminCollectionSummaryDto[] | null;
  onChange: (patch: Partial<ProductEditorForm>) => void;
}

export function DetailsTab({ slug, kind, form, categories, tagOptions, collections, onChange }: DetailsTabProps) {
  const attributesError = validateAttributesJson(form.attributesJson);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-[family-name:var(--font-mono)] text-[12px] text-muted-foreground">
          {slug}
        </span>
        <Pill tone="muted">
          {kind}
        </Pill>
        <span className="text-[11px] text-muted-foreground">
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
          <NativeSelect
            className="h-8"
            value={form.status}
            onChange={(e) => onChange({ status: e.target.value })}
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
          </NativeSelect>
        </Field>

        <Field label="Category" className="flex-1">
          <NativeSelect
            className="h-8"
            value={form.categoryId ?? ''}
            onChange={(e) => onChange({ categoryId: e.target.value || null })}
          >
            <option value="">Uncategorised</option>
            {/* When the category list failed to load, the product's own category is still a
                fact — showing only "Uncategorised" would misreport stored state and let a
                save silently clear it. Falls back to the raw id until labels arrive. */}
            {form.categoryId && !categories.some((c) => c.id === form.categoryId) && (
              <option value={form.categoryId}>{form.categoryId}</option>
            )}
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
                {category.isActive ? '' : ' (retired)'}
              </option>
            ))}
          </NativeSelect>
        </Field>
      </div>

      <Field label="Tags">
        <div className="flex flex-wrap gap-2">
          {form.tags.map((tag) => (
            <span key={tag} className="rounded-md bg-muted px-2 py-1 text-xs">
              {tagOptions?.find((option) => option.value === tag)?.label ?? tag}
              {tagOptions && !tagOptions.some((option) => option.value === tag) && ' (retired or legacy)'}
              <button type="button" aria-label={`Remove ${tag}`} className="ml-2 text-muted-foreground hover:text-destructive"
                onClick={() => onChange({ tags: form.tags.filter((value) => value !== tag) })}>×</button>
            </span>
          ))}
        </div>
        <NativeSelect value="" disabled={tagOptions === null}
          onChange={(event) => { if (event.target.value) onChange({ tags: [...form.tags, event.target.value] }); }}>
          <option value="">Add a configured tag</option>
          {tagOptions?.filter((option) => !form.tags.includes(option.value)).map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </NativeSelect>
        <span className="text-[11px] text-muted-foreground">
          {tagOptions === null ? 'Tag choices are unavailable. Existing tags are preserved.' : 'Choices come from the active tag facets. Under 500 kcal is calculated from nutrition.'}
        </span>
      </Field>

      <Field label="Components line">
        <input value={form.componentsLine} maxLength={500} className={inputClass}
          onChange={(event) => onChange({ componentsLine: event.target.value })} />
      </Field>

      <Field label="Heat">
        <NativeSelect value={form.heat ?? ''} onChange={(event) => onChange({ heat: event.target.value === '' ? null : Number(event.target.value) })}>
          <option value="">Not authored</option>
          {['None', 'Mild', 'Medium', 'Hot'].map((label, value) => <option key={value} value={value}>{label}</option>)}
        </NativeSelect>
      </Field>

      <div className="flex gap-3">
        <Field label="Low sugar" className="flex-1">
          <NativeSelect value={form.lowSugar === null ? '' : String(form.lowSugar)}
            onChange={(event) => onChange({ lowSugar: event.target.value === '' ? null : event.target.value === 'true' })}>
            <option value="">Not authored</option><option value="true">Yes</option><option value="false">No</option>
          </NativeSelect>
        </Field>
        <Field label="Freezable" className="flex-1">
          <NativeSelect value={form.freezable === null ? '' : String(form.freezable)}
            onChange={(event) => onChange({ freezable: event.target.value === '' ? null : event.target.value === 'true' })}>
            <option value="">Not authored</option><option value="true">Yes</option><option value="false">No</option>
          </NativeSelect>
        </Field>
      </div>

      <Field label="Shelf life and storage conditions">
        <textarea value={form.shelfLife} maxLength={1000} rows={3} className={inputClass}
          onChange={(event) => onChange({ shelfLife: event.target.value })} />
        <span className="text-[11px] text-muted-foreground">Include the storage conditions and when the stated shelf life begins.</span>
      </Field>

      <Field label="Related dishes collection">
        <NativeSelect value={form.relatedCollectionId ?? ''} disabled={collections === null}
          onChange={(event) => onChange({ relatedCollectionId: event.target.value || null })}>
          <option value="">None</option>
          {form.relatedCollectionId && !collections?.some((collection) => collection.id === form.relatedCollectionId) &&
            <option value={form.relatedCollectionId}>{form.relatedCollectionId}</option>}
          {collections?.map((collection) => <option key={collection.id} value={collection.id}>
            {collection.title}{collection.isActive ? '' : ' (retired)'}
          </option>)}
        </NativeSelect>
        <span className="text-[11px] text-muted-foreground">The collection’s existing order controls the related dishes. Only active collections are published.</span>
      </Field>

      <label className="flex items-start gap-2 rounded-md border border-border p-3 text-sm">
        <Checkbox checked={!form.isPlaceholder} onCheckedChange={(checked) => onChange({ isPlaceholder: checked !== true })} />
        <span>Product content and photography are approved
          <span className="mt-1 block text-[11px] text-muted-foreground">Leave unchecked while any product information or imagery is placeholder content. Uploading an image does not approve the product.</span>
        </span>
      </label>

      <Field label="Attributes JSON">
        <textarea
          value={form.attributesJson}
          onChange={(e) => onChange({ attributesJson: e.target.value })}
          rows={5}
          spellCheck={false}
          className={`${inputClass} font-[family-name:var(--font-mono)] text-[12px]`}
        />
        <p className="mt-1 text-[11px] text-muted-foreground">
          The attribute contract facet groups match on — paths traverse from this JSON's root.
        </p>
        {attributesError && (
          <p className="mt-1 text-[11px] text-destructive">{attributesError}</p>
        )}
      </Field>
    </div>
  );
}

// ─── Small shared editor pieces ────────────────────────────────────────────

export const inputClass =
  'w-full rounded-md border border-border bg-card px-2.5 py-1.5 text-[13px] text-foreground outline-none focus:border-primary';

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
      <span className="text-xs font-medium text-muted-foreground">
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
  const commit = (input: HTMLInputElement) => {
    const value = input.value.trim();
    // Duplicates are dropped rather than stored twice — the server replaces the whole list,
    // so a duplicate would persist.
    if (value && !values.includes(value)) onChange([...values, value]);
    input.value = '';
  };

  return (
    <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-border bg-card p-1.5">
      {values.map((value, index) => (
        <span
          key={`${value}-${index}`}
          className="flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-[11.5px] text-foreground"
        >
          {value}
          <button
            type="button"
            aria-label={`Remove ${value}`}
            onClick={() => onChange(values.filter((_, i) => i !== index))}
            className="text-muted-foreground hover:text-destructive"
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
          commit(e.currentTarget);
        }}
        // Also on blur: text typed and then left by clicking Save or switching tabs is
        // visible on screen, so saving without it would report success while discarding
        // something the operator could plainly see. Blur fires before the Save click lands.
        onBlur={(e) => commit(e.currentTarget)}
      />
    </div>
  );
}
