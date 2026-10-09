// Commerce storefront service (Spec 073 §4) — size plans, the fulfilment
// calendar, merchandising (collections/facets), the storefront-config document,
// the public reads the admin uses for previews, and the admin storefront
// projections (orders/carts/party) that Specs 081/083 render.

import { api } from '@/lib/api';
import type { PagedResult } from '@/types';
import type {
  AdminCartDetailDto,
  AdminCartRowDto,
  AdminCollectionDto,
  AdminCollectionSummaryDto,
  AdminOrderStorefrontDto,
  AdminOrderPackingDto,
  OrderFulfilmentDto,
  AdminPartyStorefrontDto,
  AdminStorefrontOrderRowDto,
  BoxPlanDto,
  CategoryTreeNodeDto,
  CommercePagedResult,
  ExtrasListDto,
  FacetGroupDto,
  FulfilmentCalendarDto,
  FulfilmentPromiseDto,
  StorefrontConfigDto,
  UpdateStorefrontConfigRequest,
} from '@/types/commerce';
import { normalizeCommercePage } from '@/types/commerce';

export interface BundleSizePresetRequest {
  size: number;
  price: number;
  badge?: string | null;
  blurb?: string | null;
  savingAmount?: number | null;
  sortOrder?: number;
}

/** Full replace of a bundle's size plan (Spec 068 §11). */
export interface UpsertBundleSizePlanRequest {
  minSize: number;
  maxSize: number;
  baseSize: number;
  basePrice: number;
  perSpacePrice: number;
  currency: string;
  presets: BundleSizePresetRequest[];
}

/** Full replace of the tenant's calendar (Spec 069 §6). */
export interface UpsertFulfilmentCalendarRequest {
  timezone: string;
  deliveryDays: string[];
  cutoffLocalTime: string;
  cutoffDayOfWeek?: string | null;
  leadDays: number;
  blackoutDates: string[];
  isActive: boolean;
}

export interface CreateCollectionRequest {
  slug: string;
  title: string;
  subtitle?: string | null;
  kind?: string;
  sortOrder?: number;
}

export interface UpdateCollectionRequest {
  title: string;
  subtitle?: string | null;
  clearSubtitle?: boolean;
  kind?: string | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface CollectionItemLine {
  productId: string;
  rank: number;
}

export interface CreateFacetGroupRequest {
  key: string;
  label: string;
  matchKind: string;
  optionsJson: string;
  sourcePath?: string | null;
  sortOrder?: number;
}

export interface UpdateFacetGroupRequest {
  label: string;
  optionsJson?: string | null;
  sourcePath?: string | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface ListAdminStorefrontOrdersParams {
  page?: number;
  pageSize?: number;
  paymentStatus?: string;
}

export interface ListAdminCartsParams {
  page?: number;
  pageSize?: number;
  status?: string;
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.append(key, String(value));
  }
  const s = query.toString();
  return s ? `?${s}` : '';
}

export const commerceStorefrontService = {
  // ── Size plans (Specs 068/076) ────────────────────────────────────────────
  /** Status-agnostic admin read by product id — Draft bundles included. Null-safe 404. */
  getSizePlan: async (productId: string): Promise<BoxPlanDto> =>
    api.get<BoxPlanDto>(`/commerce/admin/products/${productId}/size-plan`),
  /** The PUBLIC read by slug (Active bundles only) — what the storefront serves,
   * for the Spec 076 preview; never a substitute for the admin-by-id read. */
  getPublicBoxPlan: async (slug: string): Promise<BoxPlanDto> =>
    api.get<BoxPlanDto>(`/commerce/catalog/products/${slug}/box-plan`),
  upsertSizePlan: async (productId: string, data: UpsertBundleSizePlanRequest): Promise<BoxPlanDto> =>
    api.put<BoxPlanDto>(`/commerce/admin/products/${productId}/size-plan`, data),

  // ── Fulfilment calendar (Specs 069/077) ───────────────────────────────────
  getFulfilmentCalendar: async (config?: object): Promise<FulfilmentCalendarDto> =>
    api.get<FulfilmentCalendarDto>('/commerce/admin/fulfilment-calendar', config),
  upsertFulfilmentCalendar: async (data: UpsertFulfilmentCalendarRequest, config?: object): Promise<FulfilmentCalendarDto> =>
    api.put<FulfilmentCalendarDto>('/commerce/admin/fulfilment-calendar', data, config),

  // ── Merchandising (Specs 070/078) ─────────────────────────────────────────
  listCollections: async (config?: object): Promise<AdminCollectionSummaryDto[]> =>
    api.get<AdminCollectionSummaryDto[]>('/commerce/admin/collections', config),
  /** Extras-collection rows arrive pricing-enriched (isPriceable) — Spec 078 dependency. */
  getCollection: async (collectionId: string, config?: object): Promise<AdminCollectionDto> =>
    api.get<AdminCollectionDto>(`/commerce/admin/collections/${collectionId}`, config),
  createCollection: async (data: CreateCollectionRequest, config?: object): Promise<AdminCollectionDto> =>
    api.post<AdminCollectionDto>('/commerce/admin/collections', data, config),
  updateCollection: async (collectionId: string, data: UpdateCollectionRequest, config?: object): Promise<AdminCollectionDto> =>
    api.put<AdminCollectionDto>(`/commerce/admin/collections/${collectionId}`, data, config),
  replaceCollectionItems: async (collectionId: string, items: CollectionItemLine[], config?: object): Promise<AdminCollectionDto> =>
    api.put<AdminCollectionDto>(`/commerce/admin/collections/${collectionId}/items`, { items }, config),
  listFacetGroups: async (config?: object): Promise<FacetGroupDto[]> =>
    api.get<FacetGroupDto[]>('/commerce/admin/facet-groups', config),
  createFacetGroup: async (data: CreateFacetGroupRequest, config?: object): Promise<FacetGroupDto> =>
    api.post<FacetGroupDto>('/commerce/admin/facet-groups', data, config),
  updateFacetGroup: async (facetGroupId: string, data: UpdateFacetGroupRequest, config?: object): Promise<FacetGroupDto> =>
    api.put<FacetGroupDto>(`/commerce/admin/facet-groups/${facetGroupId}`, data, config),

  // ── Storefront config (Specs 070/079) ─────────────────────────────────────
  updateStorefrontConfig: async (data: UpdateStorefrontConfigRequest, config?: object): Promise<StorefrontConfigDto> =>
    api.put<StorefrontConfigDto>('/commerce/admin/storefront-config', data, config),

  // ── Public reads used for previews ────────────────────────────────────────
  getPublicStorefrontConfig: async (config?: object): Promise<StorefrontConfigDto> =>
    api.get<StorefrontConfigDto>('/commerce/config/storefront', config),
  /** The public delivery config is the fulfilment PROMISE (earliest date +
   * timezone), not the display amounts — those live on the storefront config. */
  getPublicDelivery: async (config?: object): Promise<FulfilmentPromiseDto> =>
    api.get<FulfilmentPromiseDto>('/commerce/config/delivery', config),
  /** What the public extras rail serves right now, plus how many members it skipped. */
  getPublicExtras: async (config?: object): Promise<ExtrasListDto> =>
    api.get<ExtrasListDto>('/commerce/catalog/extras', config),
  getPublicCategoryTree: async (): Promise<CategoryTreeNodeDto[]> =>
    api.get<CategoryTreeNodeDto[]>('/commerce/catalog/categories'),

  // ── Admin storefront projections (Specs 081/083) ──────────────────────────
  listStorefrontOrders: async (
    params: ListAdminStorefrontOrdersParams = {},
  ): Promise<PagedResult<AdminStorefrontOrderRowDto>> => {
    const raw = await api.get<CommercePagedResult<AdminStorefrontOrderRowDto>>(
      `/commerce/admin/orders${buildQuery({ ...params })}`,
    );
    return normalizeCommercePage(raw);
  },
  getStorefrontOrder: async (orderId: string, config?: object): Promise<AdminOrderStorefrontDto> =>
    api.get<AdminOrderStorefrontDto>(`/commerce/admin/orders/${orderId}/storefront`, config),

  updateOrderFulfilment: async (orderId: string, status: string, expectedVersion: string, config?: object): Promise<OrderFulfilmentDto> =>
    api.put<OrderFulfilmentDto>(`/commerce/admin/orders/${orderId}/fulfilment`, { status, expectedVersion }, config),
  getOrderPacking: async (orderId: string, signal?: AbortSignal): Promise<AdminOrderPackingDto> =>
    api.get<AdminOrderPackingDto>(`/commerce/admin/orders/${orderId}/packing`, { signal }),
  listCarts: async (params: ListAdminCartsParams = {}): Promise<PagedResult<AdminCartRowDto>> => {
    const raw = await api.get<CommercePagedResult<AdminCartRowDto>>(
      `/commerce/admin/carts${buildQuery({ ...params })}`,
    );
    return normalizeCommercePage(raw);
  },
  getCart: async (cartId: string): Promise<AdminCartDetailDto> =>
    api.get<AdminCartDetailDto>(`/commerce/admin/carts/${cartId}`),
  /** An arbitrary party's storefront summary — the unified Customers Commerce tab (Spec 081). */
  getPartyStorefront: async (partyId: string): Promise<AdminPartyStorefrontDto> =>
    api.get<AdminPartyStorefrontDto>(`/commerce/admin/parties/${partyId}/storefront`),
};
