// Commerce DTO mirrors (Spec 073 §4) — hand-written camelCase reflections of
// the C# records in src/Aonik.Commerce/Contracts/Models and the admin
// storefront projections in Contracts/Models/Checkout/AdminStorefrontModels.cs.
// Amounts are plain number decimals in the tenant currency; the admin displays
// them with the currency code the DTO carries and never converts.

import type { PagedResult } from '@/types';

// ─── Pagination ─────────────────────────────────────────────────────────────

/**
 * The Commerce API's paging envelope (`PagedResult<T>` in
 * Contracts/Models/Catalog/CatalogDtos.cs): `{ items, totalCount, page,
 * pageSize }`. This is NOT the app-wide `PagedResult` (`pageNumber` /
 * `totalPages`) — never cast one to the other; run list responses through
 * {@link normalizeCommercePage} before handing them to DataTablePagination.
 */
export interface CommercePagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Maps the Commerce envelope onto the app-wide shape (page → pageNumber, totalPages computed). */
export function normalizeCommercePage<T>(raw: CommercePagedResult<T>): PagedResult<T> {
  const pageSize = raw.pageSize > 0 ? raw.pageSize : 1;
  return {
    items: raw.items,
    totalCount: raw.totalCount,
    pageNumber: raw.page,
    pageSize: raw.pageSize,
    totalPages: Math.max(1, Math.ceil(raw.totalCount / pageSize)),
  };
}

// ─── Catalog: products (Specs 070/082) ──────────────────────────────────────

export interface ProductPriceDto {
  id: string;
  productVariantId: string;
  currency: string;
  amount: number;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  isActive: boolean;
}

export interface ProductVariantDto {
  id: string;
  productId: string;
  sku: string;
  name: string;
  optionsJson: string;
  weightGrams: number | null;
  isActive: boolean;
  prices: ProductPriceDto[];
}

export interface ProductMediaDto {
  id: string;
  url: string;
  kind: string;
  sortOrder: number;
}

export interface BundleSlotOptionDto {
  id: string;
  productVariantId: string;
  priceDelta: number | null;
}

export interface BundleSlotDto {
  id: string;
  name: string;
  minItems: number;
  maxItems: number;
  fromCategoryId: string | null;
  allowDuplicates: boolean;
  sortOrder: number;
  options: BundleSlotOptionDto[];
}

export interface ProductCategoryDto {
  id: string;
  slug: string;
  name: string;
  parentCategoryId: string | null;
  sortOrder: number;
  isActive: boolean;
}

/** Menu-grid list row — deliberately carries no retail price (Spec 070 §8). */
export interface ProductSummaryDto {
  id: string;
  slug: string;
  name: string;
  status: string;
  kind: string;
  categoryId: string | null;
  variantCount: number;
  heroImageUrl: string | null;
  tags: string[];
  attributesJson: string;
  unitSurcharge: number | null;
}

/** The full public product detail — what create/get product endpoints return.
 * Structurally CANNOT carry search keywords; that is the admin detail's job. */
export interface ProductDto {
  id: string;
  slug: string;
  name: string;
  description: string;
  status: string;
  kind: string;
  categoryId: string | null;
  tagsJson: string;
  attributesJson: string;
  bundlePricingMode: string | null;
  bundleFixedAmount: number | null;
  bundlePremium: number | null;
  bundleCurrency: string | null;
  targetMarginPct: number | null;
  variants: ProductVariantDto[];
  media: ProductMediaDto[];
  bundleSlots: BundleSlotDto[];
  effectiveOptionGroups: EffectiveOptionGroupDto[];
  unitSurcharge: number | null;
  unitSurchargeCurrency: string | null;
  content: ResolvedContentDto | null;
  contentVersion: number | null;
}

/** The admin product detail (Spec 070 §7) — ProductDto plus flat search keywords. */
export interface AdminProductDetailDto extends ProductDto {
  searchKeywords: string[];
}

// ─── Catalog: option groups & personalisation (Specs 066/074) ───────────────

export interface OptionChoiceDto {
  id: string;
  key: string;
  label: string;
  note: string | null;
  /** ABSOLUTE per-unit price (Spec 066 §8) — deltas are derived against the effective default. */
  price: number;
  isRecommendedDefault: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface OptionGroupDto {
  id: string;
  key: string;
  label: string;
  helpText: string | null;
  selectionMode: string;
  currency: string;
  sortOrder: number;
  isActive: boolean;
  choices: OptionChoiceDto[];
}

export interface EffectiveOptionChoiceDto {
  key: string;
  label: string;
  note: string | null;
  price: number;
  sortOrder: number;
}

export interface EffectiveOptionGroupDto {
  key: string;
  label: string;
  helpText: string | null;
  selectionMode: string;
  currency: string;
  sortOrder: number;
  defaultChoiceKey: string;
  choices: EffectiveOptionChoiceDto[];
}

/**
 * The STORED narrowing line as authored (Spec 074 dependency read) —
 * `allowedChoiceKeys: null` means inherit every active choice, including
 * future ones; a list means pinned. The effective view loses this distinction.
 */
export interface ProductNarrowingLineDto {
  groupKey: string;
  allowedChoiceKeys: string[] | null;
  defaultChoiceKey: string | null;
  selectionModeOverride: string | null;
  sortOrder: number;
}

export interface RecommendedDefaultChangeResult {
  group: OptionGroupDto;
  affectedProductSlugs: string[];
}

// ─── Catalog: option-dependent content (Specs 067/075) ──────────────────────

export interface NutritionDto {
  kcal: number | null;
  proteinGrams: number | null;
  carbsGrams: number | null;
  fatGrams: number | null;
  fibreGrams: number | null;
  sugarsGrams: number | null;
  saltGrams: number | null;
}

export interface HeatingStepDto {
  method: string;
  body: string;
}

/** The public §5 resolution — facts are authored or WITHHELD, never substituted. */
export interface ResolvedContentDto {
  servingLabel: string;
  nutrition: NutritionDto;
  ingredients: string | null;
  allergens: string | null;
  declarationsWithheld: boolean;
  heating: HeatingStepDto[];
  heatingWithheld: boolean;
  isStandardPreparation: boolean;
  isStale: boolean;
  canonicalSelectionJson: string;
  matchedVariantSelectionJson: string | null;
  contentVersion: number;
}

export interface ProductContentDto {
  productId: string;
  servingLabel: string;
  nutrition: NutritionDto;
  ingredients: string | null;
  allergens: string | null;
  heating: HeatingStepDto[];
  describesSelectionJson: string;
  requiresReview: boolean;
  contentVersion: number;
}

export interface ProductContentVariantDto {
  id: string;
  productId: string;
  selectionJson: string;
  servingLabel: string;
  nutrition: NutritionDto;
  ingredients: string | null;
  allergens: string | null;
  heating: HeatingStepDto[] | null;
  isActive: boolean;
}

/** The RAW authoring read (Spec 075 dependency) — block as stored + server-computed staleness. */
export interface AdminProductContentDto {
  block: ProductContentDto | null;
  isStale: boolean;
  variants: ProductContentVariantDto[];
}

export interface ContentStatusRowDto {
  productId: string;
  slug: string;
  name: string;
  productStatus: string;
  hasBlock: boolean;
  requiresReview: boolean;
  isStale: boolean;
  variantCount: number;
}

export interface ContentCoverageEntryDto {
  variantId: string;
  selectionJson: string;
  isActive: boolean;
}

export interface ContentCoverageGapDto {
  groupKey: string;
  choiceKey: string;
  selectionJson: string;
}

export interface ContentCoverageDto {
  productId: string;
  authored: ContentCoverageEntryDto[];
  singleChoiceGaps: ContentCoverageGapDto[];
}

// ─── Catalog: size-tiered box plans (Specs 068/076) ─────────────────────────

export interface BoxPlanPresetDto {
  size: number;
  price: number;
  badge: string | null;
  blurb: string | null;
  /** Authored display saving — never computed. */
  savingAmount: number | null;
  sortOrder: number;
}

/** Presets override the formula at their size; other sizes price as basePrice + (size − baseSize) × perSpacePrice. */
export interface BoxPlanDto {
  bundleProductId: string;
  minSize: number;
  maxSize: number;
  baseSize: number;
  basePrice: number;
  perSpacePrice: number;
  currency: string;
  presets: BoxPlanPresetDto[];
}

// ─── Fulfilment calendar (Specs 069/077) ────────────────────────────────────

export interface FulfilmentPromiseDto {
  /** ISO date (yyyy-MM-dd). */
  earliestDeliveryDate: string;
  timezone: string;
}

export interface FulfilmentCalendarDto {
  timezone: string;
  deliveryDays: string[];
  /** Local time (HH:mm:ss). */
  cutoffLocalTime: string;
  cutoffDayOfWeek: string | null;
  leadDays: number;
  blackoutDates: string[];
  isActive: boolean;
  /** The promise this calendar computes right now (A5 echo). Null when unresolvable. */
  currentPromise: FulfilmentPromiseDto | null;
}

// ─── Merchandising: collections, facets, categories (Specs 070/078) ─────────

export interface AdminCollectionSummaryDto {
  id: string;
  slug: string;
  title: string;
  subtitle: string | null;
  kind: string;
  sortOrder: number;
  isActive: boolean;
  itemCount: number;
}

/**
 * Extras-collection rows carry the pricing enrichment (Spec 078 dependency):
 * `isPriceable` true = priced member of the public rail; false = ACTIVE member
 * the public read skips (no price in the tenant currency); null = not
 * evaluated (not the extras collection, or the member is not Active).
 */
export interface AdminCollectionItemDto {
  productId: string;
  slug: string;
  name: string;
  status: string;
  rank: number;
  unitPrice: number | null;
  currency: string | null;
  isPriceable: boolean | null;
}

export interface AdminCollectionDto {
  id: string;
  slug: string;
  title: string;
  subtitle: string | null;
  kind: string;
  sortOrder: number;
  isActive: boolean;
  items: AdminCollectionItemDto[];
}

export interface FacetOptionDto {
  value: string;
  label: string;
  min: number | null;
  max: number | null;
}

export interface FacetGroupDto {
  id: string;
  key: string;
  label: string;
  matchKind: string;
  sourcePath: string | null;
  sortOrder: number;
  isActive: boolean;
  options: FacetOptionDto[];
}

export interface CategoryTreeNodeDto {
  id: string;
  slug: string;
  name: string;
  sortOrder: number;
  children: CategoryTreeNodeDto[];
}

// ─── Storefront config (Specs 070/079) ──────────────────────────────────────

export interface StorefrontDeliveryDto {
  listAmount: number;
  chargedAmount: number;
}

export interface StorefrontBoxPresetDto {
  size: number;
  price: number;
  badge: string | null;
  blurb: string | null;
  saving: number | null;
}

export interface StorefrontBoxPlanDto {
  minSize: number;
  maxSize: number;
  currency: string;
  perSpacePrice: number | null;
  presets: StorefrontBoxPresetDto[];
}

export interface StorefrontConfigDto {
  currency: string;
  recommendedChoiceLabel: string;
  resultsPageSize: number;
  /** Storefront-defined JSON served verbatim, e.g. {"type":"cardIndex","value":10}. */
  backToTopTrigger: unknown;
  delivery: StorefrontDeliveryDto;
  defaultBoxSlug: string | null;
  extrasCollectionSlug: string | null;
  box: StorefrontBoxPlanDto | null;
}

export interface UpdateStorefrontConfigRequest {
  recommendedChoiceLabel?: string | null;
  resultsPageSize?: number | null;
  backToTopTriggerJson?: string | null;
  deliveryListAmount?: number | null;
  deliveryChargedAmount?: number | null;
  defaultBoxSlug?: string | null;
  extrasCollectionSlug?: string | null;
}

// ─── Extras rail preview (Spec 071 public read) ─────────────────────────────

export interface ExtraRowDto {
  productId: string;
  productVariantId: string;
  slug: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  tags: string[];
  attributesJson: string | null;
  unitPrice: number;
  unitSurcharge: number | null;
  currency: string;
  /** Resolved standard-preparation content (Spec 067), when authored. */
  content: ResolvedContentDto | null;
  /** The product's effective option groups, so the picker renders without a second call. */
  optionGroups: EffectiveOptionGroupDto[];
}

export interface ExtrasListDto {
  rows: ExtraRowDto[];
  /** Active members omitted from the rail for want of a price — surfaced, never silent. */
  skipped: number;
}

// ─── Admin storefront projections (Specs 081/083 dependency endpoints) ──────

export interface AdminStorefrontOrderRowDto {
  orderId: string;
  /** "party" | "guest" — from the checked-out cart's buyer binding. */
  buyerKind: string;
  buyerPartyId: string | null;
  placedAtUtc: string;
  orderStatus: string;
  paymentStatus: string;
  /** Derived from the spine status: Fulfilled / Cancelled / Unfulfilled. */
  fulfilmentStatus: string;
  currency: string;
  total: number;
  boxSize: number | null;
}

export interface AdminOrderStorefrontItemDto {
  itemType: string;
  name: string;
  sku: string | null;
  quantity: number | null;
  unitPrice: number | null;
  amount: number;
  isAddOn: boolean;
  isDeliveryFee: boolean;
}

export interface AdminOrderChargeDto {
  subtotal: number;
  discountTotal: number;
  discountCode: string | null;
  taxTotal: number;
  total: number;
  currency: string;
}

/** Kitchen-landing selection snapshot (per box slot line). */
export interface StorefrontOrderSelectionDto {
  productVariantId: string;
  quantity: number;
  sku: string;
  personalisationSummary: string | null;
}

export interface AdminOrderStorefrontDto {
  orderId: string;
  buyerKind: string;
  buyerPartyId: string | null;
  placedAtUtc: string;
  orderStatus: string;
  paymentStatus: string;
  fulfilmentStatus: string;
  items: AdminOrderStorefrontItemDto[];
  selections: StorefrontOrderSelectionDto[];
  charge: AdminOrderChargeDto;
  boxSize: number | null;
}

export interface AdminCartBoxMetaDto {
  size: number;
  filled: number;
  /** Computed, never persisted: an OPEN box cart is checkout-blocked right now
   * (a line unavailable or an add-on price changed). Frozen carts are false. */
  drift: boolean;
}

export interface AdminCartRowDto {
  cartId: string;
  buyerKind: string;
  buyerPartyId: string | null;
  status: string;
  currency: string;
  itemCount: number;
  total: number;
  boxMeta: AdminCartBoxMetaDto | null;
  orderId: string | null;
  updatedAtUtc: string;
}

/** One customer-visible change from renormalising a stored selection (Spec 066 §7). */
export interface SelectionDriftDto {
  groupKey: string;
  fromChoiceKey: string | null;
  toChoiceKey: string | null;
  /** option-retired | group-removed | group-added | selection-mode-changed | currency-mismatch */
  reason: string;
}

export interface AdminCartLineDto {
  lineId: string;
  kind: string;
  name: string;
  sku: string;
  quantity: number;
  unitPriceSnapshot: number;
  personalisationSummary: string | null;
  /** The STORED canonical selection; null when unpersonalised. */
  selectionJson: string | null;
  /** Computed read-only at load time (live editable carts only) — nothing is persisted. */
  isUnavailable: boolean;
  /** The line's charge would change on the customer's next continue (retail or personalisation repricing). */
  priceChanged: boolean;
  /** Per-line drift reasons from the Spec 066 renormalisation; empty when nothing drifted. */
  selectionDrift: SelectionDriftDto[];
}

export interface AdminCartDetailDto {
  cartId: string;
  buyerKind: string;
  buyerPartyId: string | null;
  status: string;
  currency: string;
  boxMeta: AdminCartBoxMetaDto | null;
  orderId: string | null;
  updatedAtUtc: string;
  lines: AdminCartLineDto[];
}

export interface StorefrontOrderSummaryDto {
  orderId: string;
  placedAtUtc: string;
  status: string;
  currency: string;
  total: number;
  boxSize: number | null;
}

export interface AdminPartyActiveCartDto {
  cartId: string;
  size: number;
  filled: number;
}

/** The Spec 081 Commerce-tab read. `adopted` is the recorded fact only. */
export interface AdminPartyStorefrontDto {
  orders: StorefrontOrderSummaryDto[];
  activeCart: AdminPartyActiveCartDto | null;
  adopted: boolean;
}
