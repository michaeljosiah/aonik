// Touched-field PATCH construction for the product editor (Spec 082 §3). Pure, so the
// rules that decide what reaches the wire are testable without a browser.
//
// Two contract asymmetries make this more than a diff, and both lose data silently if
// handled loosely:
//   * keywords READ as `searchKeywords: string[]` on the admin detail but WRITE as
//     `searchKeywordsJson` — the array member does not exist on the write contract;
//   * media replace takes position as the order — no sort field exists on the wire, so a
//     client-side sortOrder would be silently ignored and persist the wrong order.

import type { PatchProductRequest, ProductMediaLine } from '@/services/commerceCatalogService';
import type { AdminProductDetailDto } from '@/types/commerce';

/** The editable shape the Details/Storefront tabs bind to. */
export interface ProductEditorForm {
  name: string;
  description: string;
  status: string;
  categoryId: string | null;
  tags: string[];
  attributesJson: string;
  searchKeywords: string[];
}

/** The form as the server currently has it — the baseline every diff is taken against. */
export function formFromProduct(product: AdminProductDetailDto): ProductEditorForm {
  return {
    name: product.name ?? '',
    description: product.description ?? '',
    status: product.status ?? '',
    categoryId: product.categoryId ?? null,
    tags: parseJsonArray(product.tagsJson),
    attributesJson: product.attributesJson ?? '{}',
    searchKeywords: product.searchKeywords ?? [],
  };
}

/**
 * Only what actually changed. An untouched member must never appear in the payload —
 * PATCH is partial, so sending an unchanged value is at best noise and at worst clobbers a
 * concurrent edit.
 */
export function buildProductPatch(
  original: ProductEditorForm,
  edited: ProductEditorForm,
): PatchProductRequest {
  const patch: PatchProductRequest = {};

  if (edited.name !== original.name) patch.name = edited.name;
  if (edited.description !== original.description) patch.description = edited.description;
  if (edited.status !== original.status) patch.status = edited.status;
  if (edited.attributesJson !== original.attributesJson) patch.attributesJson = edited.attributesJson;

  // Category: null is a real value (uncategorised), so "cleared" is its own flag rather
  // than an absent member — an absent member means untouched.
  if (edited.categoryId !== original.categoryId) {
    if (edited.categoryId === null) {
      patch.clearCategory = true;
    } else {
      patch.categoryId = edited.categoryId;
    }
  }

  if (!sameStringList(edited.tags, original.tags)) {
    patch.tagsJson = JSON.stringify(edited.tags);
  }

  // Keywords write as JSON — the complete edited array, because the server replaces rather
  // than merges. Order-insensitive comparison would be wrong: the operator's ordering is
  // part of what they authored.
  if (!sameStringList(edited.searchKeywords, original.searchKeywords)) {
    patch.searchKeywordsJson = JSON.stringify(edited.searchKeywords);
  }

  return patch;
}

/** True when the patch would send nothing — the caller skips the request entirely. */
export function isEmptyPatch(patch: PatchProductRequest): boolean {
  return Object.keys(patch).length === 0;
}

/**
 * Media lines in DISPLAY ORDER. Deliberately carries no sort field: the server assigns sort
 * from array position, so inventing one client-side would be ignored and mislead the next
 * reader into thinking order was being sent explicitly.
 */
export function buildMediaReplacement(
  items: readonly { url: string; kind?: string | null }[],
): ProductMediaLine[] {
  return items
    .filter((item) => item.url.trim().length > 0)
    .map((item) => ({ url: item.url.trim(), kind: item.kind ?? null }));
}

/** Moves one media entry, returning a new array — position is the order that gets saved. */
export function moveItem<T>(items: readonly T[], from: number, to: number): T[] {
  if (from === to || from < 0 || to < 0 || from >= items.length || to >= items.length) {
    return [...items];
  }
  const next = [...items];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

/** Client-side attributes validation — the contract facet groups match paths against. */
export function validateAttributesJson(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;   // empty is allowed; the caller sends "{}"
  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    return 'Attributes must be valid JSON.';
  }
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return 'Attributes must be a JSON object — facet paths traverse from its root.';
  }
  return null;
}

function parseJsonArray(json?: string | null): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === 'string') : [];
  } catch {
    // A malformed legacy value renders as empty rather than breaking the editor.
    return [];
  }
}

function sameStringList(a: readonly string[], b: readonly string[]): boolean {
  return a.length === b.length && a.every((value, index) => value === b[index]);
}
