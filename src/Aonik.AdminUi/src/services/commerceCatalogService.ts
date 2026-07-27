// Commerce catalog service (Spec 073 §4) — products, option groups and
// per-product personalisation. House pattern: hand-written calls returning
// unwrapped res.data via api.*; reads sit behind AdminUserPolicy and writes
// behind AdminWritePolicy server-side, so callers surface err.userMessage
// (403 included) rather than inventing client-side permission checks.

import { api } from '@/lib/api';
import type { PagedResult } from '@/types';
import type {
  AdminProductDetailDto,
  CommercePagedResult,
  EffectiveOptionGroupDto,
  OptionChoiceDto,
  OptionGroupDto,
  ProductCategoryDto,
  ProductDto,
  ProductMediaDto,
  ProductNarrowingLineDto,
  ProductSummaryDto,
  RecommendedDefaultChangeResult,
} from '@/types/commerce';
import { normalizeCommercePage } from '@/types/commerce';

export interface ListCommerceProductsParams {
  page?: number;
  pageSize?: number;
  status?: string;
  kind?: string;
  categoryId?: string;
  search?: string;
}

export interface CreateProductRequest {
  slug: string;
  name: string;
  kind: string;
  description?: string;
  status?: string;
  categoryId?: string | null;
  tagsJson?: string;
  attributesJson?: string;
}

/** PATCH semantics — omitted members leave the stored value unchanged (Spec 070 §7).
 * Target margin is deliberately NOT here: the PATCH endpoint's request has no such
 * member (the JSON would be silently ignored) — use setTargetMargin. */
export interface PatchProductRequest {
  name?: string;
  description?: string;
  status?: string;
  categoryId?: string | null;
  clearCategory?: boolean;
  tagsJson?: string;
  attributesJson?: string;
  searchKeywordsJson?: string;
}

export interface TargetMarginDto {
  productId: string;
  productName: string;
  targetMarginPct: number | null;
}

export interface ProductMediaLine {
  url: string;
  kind?: string | null;
}

export interface CreateOptionGroupRequest {
  key: string;
  label: string;
  helpText?: string | null;
  selectionMode?: string;
  currency?: string;
  sortOrder?: number;
}

/**
 * Key is immutable by design (Spec 066). Two different semantics here, and the
 * distinction is load-bearing: the OPTIONAL members preserve the stored value
 * when omitted, but `label` and `helpText` are full-text-state — the server
 * assigns them unconditionally, so an omitted `helpText` binds to null and
 * ERASES the stored text. Both are therefore required: send the current value.
 */
export interface UpdateOptionGroupRequest {
  label: string;
  /** Required: send the current text (or null to clear) — omitting it erases. */
  helpText: string | null;
  selectionMode?: string | null;
  currency?: string | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface AddOptionChoiceRequest {
  key: string;
  label: string;
  note?: string | null;
  price?: number;
  isRecommendedDefault?: boolean;
  sortOrder?: number;
  isActive?: boolean;
}

/** Same split as the group update: `label`/`note` are full-text-state (assigned
 * unconditionally server-side), the rest preserve on omission. */
export interface UpdateOptionChoiceRequest {
  label: string;
  /** Required: send the current note (or null to clear) — omitting it erases. */
  note: string | null;
  price?: number | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface ProductOptionGroupLine {
  groupKey: string;
  /** Null/omitted = every active choice in the group (inherit-future); a list = pinned. */
  allowedChoiceKeys?: string[] | null;
  defaultChoiceKey?: string | null;
  selectionModeOverride?: string | null;
  sortOrder?: number;
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.append(key, String(value));
  }
  const s = query.toString();
  return s ? `?${s}` : '';
}

export const commerceCatalogService = {
  // ── Products ──────────────────────────────────────────────────────────────
  listProducts: async (params: ListCommerceProductsParams = {}): Promise<PagedResult<ProductSummaryDto>> => {
    const raw = await api.get<CommercePagedResult<ProductSummaryDto>>(
      `/commerce/admin/products${buildQuery({ ...params })}`,
    );
    return normalizeCommercePage(raw);
  },
  getProduct: async (productId: string): Promise<AdminProductDetailDto> =>
    api.get<AdminProductDetailDto>(`/commerce/admin/products/${productId}`),
  /** The create endpoint returns the PUBLIC detail shape — no searchKeywords;
   * editors re-fetch getProduct before editing admin-only fields. */
  createProduct: async (data: CreateProductRequest): Promise<ProductDto> =>
    api.post<ProductDto>('/commerce/admin/products', data),
  patchProduct: async (productId: string, data: PatchProductRequest): Promise<AdminProductDetailDto> =>
    api.patch<AdminProductDetailDto>(`/commerce/admin/products/${productId}`, data),
  /** Returns the replaced media list, not a product detail. */
  replaceProductMedia: async (productId: string, items: ProductMediaLine[]): Promise<ProductMediaDto[]> =>
    api.put<ProductMediaDto[]>(`/commerce/admin/products/${productId}/media`, { items }),
  listCategories: async (): Promise<ProductCategoryDto[]> =>
    api.get<ProductCategoryDto[]>('/commerce/admin/categories'),

  // ── Option groups (tenant catalogue) ──────────────────────────────────────
  listOptionGroups: async (): Promise<OptionGroupDto[]> =>
    api.get<OptionGroupDto[]>('/commerce/admin/option-groups'),
  createOptionGroup: async (data: CreateOptionGroupRequest): Promise<OptionGroupDto> =>
    api.post<OptionGroupDto>('/commerce/admin/option-groups', data),
  updateOptionGroup: async (groupId: string, data: UpdateOptionGroupRequest): Promise<OptionGroupDto> =>
    api.put<OptionGroupDto>(`/commerce/admin/option-groups/${groupId}`, data),
  /** Both choice mutations return the SINGLE choice — refresh the group list
   * for the surrounding state rather than trusting a group echo. */
  addOptionChoice: async (groupId: string, data: AddOptionChoiceRequest): Promise<OptionChoiceDto> =>
    api.post<OptionChoiceDto>(`/commerce/admin/option-groups/${groupId}/choices`, data),
  updateOptionChoice: async (choiceId: string, data: UpdateOptionChoiceRequest): Promise<OptionChoiceDto> =>
    api.put<OptionChoiceDto>(`/commerce/admin/option-choices/${choiceId}`, data),
  /** Moves the group's recommended default; the result names every affected product (Spec 066 §9). */
  setRecommendedDefault: async (groupId: string, choiceKey: string): Promise<RecommendedDefaultChangeResult> =>
    api.put<RecommendedDefaultChangeResult>(
      `/commerce/admin/option-groups/${groupId}/recommended-default`,
      { choiceKey },
    ),

  // ── Per-product narrowing + surcharge ─────────────────────────────────────
  /** The STORED narrowing (null-vs-explicit preserved) — Spec 074's editor read. */
  getProductNarrowing: async (productId: string): Promise<ProductNarrowingLineDto[]> =>
    api.get<ProductNarrowingLineDto[]>(`/commerce/admin/products/${productId}/option-groups`),
  /** Full replace, idempotent; an empty list makes the product not personalisable.
   * Returns the RESOLVED effective groups — apply them rather than re-fetching. */
  setProductOptionGroups: async (
    productId: string,
    groups: ProductOptionGroupLine[],
  ): Promise<EffectiveOptionGroupDto[]> =>
    api.put<EffectiveOptionGroupDto[]>(`/commerce/admin/products/${productId}/option-groups`, { groups }),
  /** Set or clear the per-unit surcharge; currency required with an amount.
   * Returns the updated product detail. */
  setUnitSurcharge: async (
    productId: string,
    amount: number | null,
    currency?: string | null,
  ): Promise<ProductDto> =>
    api.put<ProductDto>(`/commerce/admin/products/${productId}/surcharge`, { amount, currency }),
  /** Target margin has its own endpoint (Spec 057) — it is NOT a product PATCH member. */
  setTargetMargin: async (productId: string, targetMarginPct: number | null): Promise<TargetMarginDto> =>
    api.put<TargetMarginDto>(`/commerce/admin/products/${productId}/target-margin`, { targetMarginPct }),
};
