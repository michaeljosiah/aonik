// Commerce content service (Spec 073 §4) — option-dependent product content
// authoring (Spec 067): the default block, per-combination variants, coverage
// and the review workflow. Editors load the RAW admin read (block as stored +
// server-computed staleness), never the public resolution, which withholds and
// substitutes by design.

import { api } from '@/lib/api';
import type { PagedResult } from '@/types';
import type {
  AdminProductContentDto,
  CommercePagedResult,
  ContentCoverageDto,
  ContentStatusRowDto,
  ProductContentDto,
  ProductContentVariantDto,
} from '@/types/commerce';
import { normalizeCommercePage } from '@/types/commerce';

/** Upsert of the default block. Null figures mean "not published", never zero. */
export interface UpsertProductContentRequest {
  servingLabel: string;
  kcal?: number | null;
  proteinGrams?: number | null;
  carbsGrams?: number | null;
  fatGrams?: number | null;
  fibreGrams?: number | null;
  sugarsGrams?: number | null;
  saltGrams?: number | null;
  ingredients?: string | null;
  allergens?: string | null;
  heatingJson?: string | null;
}

/**
 * Variant authoring. selectionJson may be partial — the server normalises it
 * through Spec 066 and stores it complete. Null declarations/heating mean
 * WITHHELD for this combination, never inherited.
 */
export interface UpsertContentVariantRequest {
  selectionJson: string;
  servingLabel: string;
  kcal?: number | null;
  proteinGrams?: number | null;
  carbsGrams?: number | null;
  fatGrams?: number | null;
  fibreGrams?: number | null;
  sugarsGrams?: number | null;
  saltGrams?: number | null;
  ingredients?: string | null;
  allergens?: string | null;
  heatingJson?: string | null;
}

export const commerceContentService = {
  /** RAW admin read: stored block + variants + server-computed isStale (Spec 075 dependency). */
  getAdminContent: async (productId: string): Promise<AdminProductContentDto> =>
    api.get<AdminProductContentDto>(`/commerce/admin/products/${productId}/content`),

  /** Tenant-wide content status rows (Spec 075 rail/KPIs/queue). */
  listContentStatus: async (page = 1, pageSize = 50): Promise<PagedResult<ContentStatusRowDto>> => {
    const raw = await api.get<CommercePagedResult<ContentStatusRowDto>>(
      `/commerce/admin/content?page=${page}&pageSize=${pageSize}`,
    );
    return normalizeCommercePage(raw);
  },

  upsertContent: async (productId: string, data: UpsertProductContentRequest): Promise<ProductContentDto> =>
    api.put<ProductContentDto>(`/commerce/admin/products/${productId}/content`, data),

  /** Clears RequiresReview after a human confirms the block still describes the new default. */
  confirmReview: async (productId: string): Promise<ProductContentDto> =>
    api.post<ProductContentDto>(`/commerce/admin/products/${productId}/content/confirm-review`),

  upsertVariant: async (productId: string, data: UpsertContentVariantRequest): Promise<ProductContentVariantDto> =>
    api.post<ProductContentVariantDto>(`/commerce/admin/products/${productId}/content-variants`, data),

  updateVariant: async (variantId: string, data: UpsertContentVariantRequest): Promise<ProductContentVariantDto> =>
    api.put<ProductContentVariantDto>(`/commerce/admin/content-variants/${variantId}`, data),

  deleteVariant: async (variantId: string): Promise<void> =>
    api.delete<void>(`/commerce/admin/content-variants/${variantId}`),

  /** Authored combinations + single-choice-deviation gaps — bounded, never combinatorial. */
  getCoverage: async (productId: string): Promise<ContentCoverageDto> =>
    api.get<ContentCoverageDto>(`/commerce/admin/products/${productId}/content-coverage`),
};
