import type { FacetOptionDto } from '@/types/commerce';

export interface FacetOptionDraft { value: string; label: string; min: string; max: string }

export function optionDraft(option?: FacetOptionDto): FacetOptionDraft {
  return { value: option?.value ?? '', label: option?.label ?? '',
    min: option?.min == null ? '' : String(option.min), max: option?.max == null ? '' : String(option.max) };
}

function bound(value: string): number | null {
  if (!value.trim()) return null;
  if (!/^-?(?:\d+(?:\.\d+)?|\.\d+)$/.test(value.trim())) throw new Error('Range bounds must be numbers, or blank for an open end.');
  const number = Number(value);
  if (!Number.isFinite(number) || Math.abs(number) > Number.MAX_SAFE_INTEGER) throw new Error('The range bound is outside the supported range.');
  return number;
}

export function serializeFacetOptions(rows: readonly FacetOptionDraft[], matchKind: string): string {
  if (rows.length === 0) throw new Error('Add at least one option.');
  const seen = new Set<string>();
  const options = rows.map((row) => {
    const value = row.value.trim();
    const label = row.label.trim();
    if (!value || !label) throw new Error('Every option needs a value and a label.');
    if (seen.has(value)) throw new Error('Option values must be unique.');
    seen.add(value);
    return matchKind === 'Range' ? { value, label, min: bound(row.min), max: bound(row.max) } : { value, label };
  });
  if (matchKind === 'Range') {
    let previousMax = -Infinity;
    options.forEach((option, index) => {
      const min = 'min' in option ? option.min : null;
      const max = 'max' in option ? option.max : null;
      if (min == null && max == null) throw new Error('Every range needs at least one bound.');
      if (min != null && max != null && min >= max) throw new Error('A range minimum must be below its maximum.');
      if (index > 0 && (min ?? -Infinity) < previousMax) throw new Error('Ranges must be ordered and must not overlap.');
      previousMax = max ?? Infinity;
    });
  }
  const json = JSON.stringify(options);
  if (json.length > 4096) throw new Error('The options are too long; shorten them or remove an option.');
  return json;
}

export function rangeLabel(min: number | null, max: number | null) {
  return `[${min ?? '−∞'}, ${max ?? '∞'})`;
}

export function facetSource(matchKind: string, value: string): string | null {
  if (matchKind !== 'Range' && matchKind !== 'Attribute') return null;
  const path = value.trim();
  if (!path || path.length > 128) throw new Error('Enter a source path of 1 to 128 characters.');
  if (['nutrition.kcal', 'nutrition.proteinGrams', 'nutrition.fibreGrams'].includes(path) && matchKind !== 'Range') {
    throw new Error('Nutrition sources use Range matching.');
  }
  if (path === 'lowSugar' && matchKind !== 'Attribute') throw new Error('lowSugar uses Attribute matching.');
  return path;
}
