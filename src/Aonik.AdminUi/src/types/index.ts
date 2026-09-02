// Navigation Types
export interface NavItemGroup {
  label: string;
  items: NavItem[];
}

export interface NavigationSection {
  id: string;
  label?: string;
  items: NavItem[];
  audience?: 'host' | 'tenant' | 'all';
}

export interface NavItem {
  id: string;
  label: string;
  icon: string;
  href?: string;
  badge?: string;
  children?: NavItem[];
  audience?: 'host' | 'tenant' | 'all';
  /**
   * Runtime-manifest gate: when a manifest is present and this id is not in
   * its enabledModules, the item (and its subtree) does not render. Absent
   * manifest → render (fail-open, matching useModules()).
   */
  moduleId?: string;
  /** Grouped children for flyout menus - takes precedence over children if present */
  childGroups?: NavItemGroup[];
  /** Footer link for the flyout menu (e.g., "View all") */
  viewAllHref?: string;
  viewAllLabel?: string;
}

// User Types
export interface User {
  id: string;
  name: string;
  role: string;
  avatar?: string;
}

// Activity Feed Types
export interface ActivityItem {
  id: string;
  title: string;
  description?: string;
  timestamp: string;
  icon?: string;
}

// Quick Link Types
export interface QuickLink {
  id: string;
  label: string;
  icon: string;
  href: string;
}

// App Card Types
export type AppStatus = 'active' | 'pending' | 'request';

export interface AppOwner {
  id: string;
  name: string;
  role?: string;
  avatar?: string;
}

export interface AppCard {
  id: string;
  name: string;
  description: string;
  icon?: string;
  iconBgColor?: string;
  status: AppStatus;
  owners: AppOwner[];
  dateModified: string;
  modifiedBy: string;
  tags: string[];
}

// Agent Card Types
export type VisibilityLevel = 'team' | 'enterprise' | 'private';

export interface AgentCard {
  id: string;
  name: string;
  description: string;
  avatar?: string;
  visibility: VisibilityLevel;
  source: string;
  skills: string[];
  plugins: string[];
  /** Optional config-level metadata for agent management pages */
  riskTier?: 'low' | 'medium' | 'high';
  isActive?: boolean;
  isOverride?: boolean;
  modelName?: string | null;
}

// Databox Types
export interface Databox {
  id: string;
  name: string;
  description: string;
  color: string;
  lastModified: string;
  modifiedBy: string;
}

// MySpace Summary Types
export interface FinancialMetricDto {
  metricKey: string;
  formattedValue: string;
  valueLabel?: string | null;
  trendDirection: 'up' | 'down' | 'neutral';
  trendPercent: number;
  sparkline: number[];
  count?: number | null;
  total?: number | null;
}

export interface ActivityItemDto {
  id: string;
  title: string;
  description?: string | null;
  timestamp: string;
  icon: string;
}

export interface MySpaceSummaryResponse {
  financialMetrics: FinancialMetricDto[];
  recentActivity: ActivityItemDto[];
  agentOpsToday: number;
  cashPositionUpdatedAt: string | null;
  cashTimeline: CashTimelineDto;
  agentProposals: AgentProposalDto[];
}

export interface CashTimelineDto {
  currency: string;
  /** Tenant's configured currency set — drives the switcher options. */
  availableCurrencies: string[];
  historical: CashTimelinePointDto[];
  projected: CashTimelinePointDto[];
  events: CashTimelineEventDto[];
  projectedLow: number | null;
  projectedLowAt: string | null;
}

export interface CashTimelinePointDto {
  date: string;
  balance: number;
}

export interface CashTimelineEventDto {
  date: string;
  kind: string;
  label: string;
  amount: number;
}

export interface AgentProposalDto {
  id: string;
  agentName: string;
  agentDomain: string;
  agentIconUrl: string | null;
  confidence: number;
  summary: string;
  reason: string | null;
  riskTier: string;
  createdAt: string;
}

/** Full detail for a proposal — returned by /ai/proposals/{id} (Wave 4c.2). */
export interface ProposalDetailResponse {
  id: string;
  proposalType: string;
  proposedByAgentId: string;
  agentName: string;
  agentDomain: string;
  agentIconUrl: string | null;
  aiRunId: string;
  summary: string;
  riskTier: string;
  confidence: number;
  /** "Proposed" | "Approved" | "Rejected" — string for forward compatibility */
  status: string;
  approvedByUserId: string | null;
  approvedAt: string | null;
  payloadJson: string;
  createdAt: string;
}

/** Compact list-row view used by the Approvals queue (Wave 6). */
export interface ProposalListItem {
  id: string;
  proposalType: string;
  agentName: string;
  agentDomain: string;
  agentIconUrl: string | null;
  confidence: number;
  summary: string;
  riskTier: string;
  createdAt: string;
}

export interface ListProposalsResponse {
  items: ProposalListItem[];
  total: number;
}

// Tenant Types
export type TenantStatus = 'Active' | 'Provisioning' | 'Deactivated' | 'Suspended';
export type TenantEnvironment = 'Dev' | 'Test' | 'Staging' | 'Prod';

export interface Tenant {
  id: string;
  tenantId: string;
  name: string;
  environment: TenantEnvironment;
  defaultCurrency: string;
  supportedCountries: string[];
  allowedOriginCountries: string[];
  allowedDestinationCountries: string[];
  supportedCurrencies: string[];
  status: TenantStatus;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  // Company Setup fields
  logoUrl?: string | null;
  industry?: string | null;
  companySize?: string | null;
  website?: string | null;
  // Contact fields
  contactEmail?: string | null;
  contactMobile?: string | null;
  // Address fields
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  stateProvince?: string | null;
  postalCode?: string | null;
  country?: string | null;
  // Setup tracking
  isSetupComplete: boolean;
  setupStep: number;
}

export interface CreateTenantRequest {
  name: string;
  environment: TenantEnvironment;
  defaultCurrency: string;
  supportedCountries: string[];
  /**
   * Email of the customer's first administrator. The backend creates a
   * pending placeholder User + Party for this email and grants them
   * `TenantAdmin`; the first IdP login matching this email links onto
   * the placeholder. Required — JIT user creation for arbitrary tenant
   * logins is no longer permitted.
   */
  ownerEmail: string;
  /**
   * Optional human-readable name for the owner; falls back to the
   * email when omitted.
   */
  ownerDisplayName?: string;
  /** Business-type config pack to apply at provision (Spec 065). Optional; omitted normalizes to "base". */
  businessType?: string;
  allowedOriginCountries?: string[];
  allowedDestinationCountries?: string[];
  supportedCurrencies?: string[];
}

export interface UpdateTenantRequest {
  name?: string;
  environment?: TenantEnvironment;
  defaultCurrency?: string;
  supportedCountries?: string[];
  allowedOriginCountries?: string[];
  allowedDestinationCountries?: string[];
  supportedCurrencies?: string[];
  // Company Setup fields
  logoUrl?: string | null;
  industry?: string | null;
  companySize?: string | null;
  website?: string | null;
  // Contact fields
  contactEmail?: string | null;
  contactMobile?: string | null;
  // Address fields
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  stateProvince?: string | null;
  postalCode?: string | null;
  country?: string | null;
  // Setup tracking
  isSetupComplete?: boolean;
  setupStep?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface TextToSpeechVoiceProfileResponse {
  provider: string;
  voiceId: string;
  modelId?: string | null;
  locale?: string | null;
  outputFormat?: string | null;
  providerOptions: Record<string, string | null>;
}

export interface TextToSpeechPolicyResponse {
  maxCharactersPerUtterance: number;
  maxRequestsPerMinutePerUser: number;
  monthlyCharacterBudget?: number | null;
}

export interface TextToSpeechSettingsResponse {
  enabled: boolean;
  fallbackToNativeOnFailure: boolean;
  defaultProfile: TextToSpeechVoiceProfileResponse;
  policy: TextToSpeechPolicyResponse;
}

export interface TextToSpeechVoiceProfileUpdateRequest {
  provider: string;
  voiceId: string;
  modelId?: string | null;
  locale?: string | null;
  outputFormat?: string | null;
  providerOptions?: Record<string, string | null>;
}

export interface TextToSpeechPolicyUpdateRequest {
  maxCharactersPerUtterance: number;
  maxRequestsPerMinutePerUser: number;
  monthlyCharacterBudget?: number | null;
}

export interface TextToSpeechSettingsUpdateRequest {
  enabled: boolean;
  fallbackToNativeOnFailure: boolean;
  defaultProfile: TextToSpeechVoiceProfileUpdateRequest;
  policy: TextToSpeechPolicyUpdateRequest;
}

export interface TextToSpeechVoiceOptionResponse {
  voiceId: string;
  name: string;
  previewUrl?: string | null;
  category?: string | null;
  labels: Record<string, string | null>;
}

export interface TextToSpeechPreviewRequest {
  text: string;
  locale?: string | null;
  provider?: string | null;
  voiceId?: string | null;
  modelId?: string | null;
  outputFormat?: string | null;
  providerOptions?: Record<string, string | null>;
}

export interface OrderListItem {
  orderId: string;
  orderType: string;
  status: string;
  payerPartyId?: string | null;
  payerName: string;
  originCountry?: string | null;
  originCurrency: string;
  totalAmountIn: number;
  totalAmountOut?: number | null;
  destinationCurrency?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentListItem {
  documentId: string;
  ownerPartyId: string;
  documentType: string;
  status: string;
  issuedOn?: string | null;
  expiresOn?: string | null;
  issuerName?: string | null;
  countryCode?: string | null;
  referenceNumber?: string | null;
  tags: string[];
  filesCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentResponse {
  documentId: string;
  ownerPartyId: string;
  documentType: string;
  status: string;
  issuedOn?: string | null;
  expiresOn?: string | null;
  issuerName?: string | null;
  countryCode?: string | null;
  referenceNumber?: string | null;
  tags: string[];
  attributesJson: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentFileResponse {
  documentFileId: string;
  documentId: string;
  storageProvider: string;
  storageContainer?: string | null;
  storageKey: string;
  contentType: string;
  fileName?: string | null;
  fileSizeBytes?: number | null;
  sha256?: string | null;
  pageIndex?: number | null;
  side?: string | null;
  capturedAt?: string | null;
  capturedBy?: string | null;
  metadataJson: string;
  createdAt: string;
}

export interface DocumentVerificationResponse {
  documentVerificationId: string;
  documentUsageId: string;
  decision: string;
  decisionReasonCode?: string | null;
  decisionNotes?: string | null;
  verifierType: string;
  verifierId?: string | null;
  aiRunId?: string | null;
  createdAt: string;
}

export interface DocumentUsageResponse {
  documentUsageId: string;
  documentId: string;
  ownerPartyId: string;
  purpose: string;
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
  status: string;
  verifiedByUserId?: string | null;
  verifiedAt?: string | null;
  notes?: string | null;
  verifications: DocumentVerificationResponse[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentVersionResponse {
  documentVersionId: string;
  documentId: string;
  version: number;
  status: string;
  submittedAt?: string | null;
  decisionedAt?: string | null;
  decisionReason?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentDetailsResponse {
  document: DocumentResponse;
  files: DocumentFileResponse[];
  usages: DocumentUsageResponse[];
  versions: DocumentVersionResponse[];
}

export interface CreateDocumentRequest {
  ownerPartyId: string;
  documentType: string;
  status?: string | null;
  issuedOn?: string | null;
  expiresOn?: string | null;
  issuerName?: string | null;
  countryCode?: string | null;
  referenceNumber?: string | null;
  tags: string[];
  attributesJson?: string | null;
}

export interface AddDocumentFileRequest {
  storageProvider: string;
  storageContainer?: string | null;
  storageKey: string;
  contentType: string;
  fileName?: string | null;
  fileSizeBytes?: number | null;
  sha256?: string | null;
  pageIndex?: number | null;
  side?: string | null;
  capturedAt?: string | null;
  capturedBy?: string | null;
  metadataJson?: string | null;
}

export interface AddDocumentUsageRequest {
  ownerPartyId: string;
  purpose: string;
  relatedEntityType?: string | null;
  relatedEntityId?: string | null;
  status?: string | null;
  notes?: string | null;
}

export interface AddDocumentVerificationRequest {
  decision: string;
  decisionReasonCode?: string | null;
  decisionNotes?: string | null;
  verifierType: string;
  verifierId?: string | null;
  aiRunId?: string | null;
}

export interface BootstrapTenantResult {
  tenantId: string;
  tenantName: string;
  tenantCreated: boolean;
  userId: string;
  userCreated: boolean;
  platformAdminAssigned: boolean;
  tenantAdminAssigned: boolean;
  ownerEmail: string;
  requiresIdentityLink: boolean;
}

export interface BootstrapStatusResponse {
  state: 'ready' | 'completed' | 'disabled' | 'misconfigured';
  bootstrapEnabled: boolean;
  setupSecretConfigured: boolean;
  tenantCount: number;
  canBootstrap: boolean;
  message?: string | null;
}

export interface CatalogCountryItem {
  countryCode: string;
  name: string;
}

export interface CatalogCountryResponse {
  countries: CatalogCountryItem[];
}

export interface CatalogCurrencyItem {
  code: string;
  name: string;
}

export interface CatalogCurrencyResponse {
  currencies: CatalogCurrencyItem[];
  defaultCurrencyCode?: string | null;
}

export interface ReferenceDataItem {
  code: string;
  displayName: string;
  sortOrder: number;
}

export interface CatalogBillerCategoryItem {
  categoryId: string;
  name: string;
  description?: string | null;
  iconUrl?: string | null;
  countryCode: string;
}

export interface CatalogBillerCategoryResponse {
  categories: CatalogBillerCategoryItem[];
}

export interface CreateCatalogBillerCategoryRequest {
  name: string;
  countryCode: string;
  description?: string | null;
  iconUrl?: string | null;
  sortOrder?: number;
  isActive?: boolean;
}

export interface UpdateCatalogBillerCategoryRequest {
  name?: string | null;
  description?: string | null;
  iconUrl?: string | null;
  sortOrder?: number | null;
  isActive?: boolean | null;
}

export interface CreateCatalogBillerRequest {
  name: string;
  countryCode: string;
  categoryId: string;
  correspondentPartnerId?: string | null;
  description?: string | null;
  logoUrl?: string | null;
  bannerUrl?: string | null;
  supportPhone?: string | null;
  supportEmail?: string | null;
  isActive?: boolean;
  isFeatured?: boolean;
  sortOrder?: number;
}

export interface UpdateCatalogBillerRequest {
  name?: string | null;
  categoryId?: string | null;
  correspondentPartnerId?: string | null;
  description?: string | null;
  logoUrl?: string | null;
  bannerUrl?: string | null;
  supportPhone?: string | null;
  supportEmail?: string | null;
  isActive?: boolean | null;
  isFeatured?: boolean | null;
  sortOrder?: number | null;
}

export interface CatalogPaginationMetadata {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CatalogBillerSummaryItem {
  billerId: string;
  name: string;
  logoUrl?: string | null;
  countryCode: string;
  categoryId: string;
  correspondentPartnerId?: string | null;
  isActive: boolean;
  isFeatured: boolean;
  // Import provenance (Spec 040). Empty sourceConnectors ⇒ manually authored.
  sourceConnectors?: string[] | null;
  providerBillerCode?: string | null;
  lastSyncedAt?: string | null;
}

export interface CatalogBillerResponse {
  billers: CatalogBillerSummaryItem[];
  pagination: CatalogPaginationMetadata;
}

export interface CatalogBillerDetailResponse {
  billerId: string;
  name: string;
  description?: string | null;
  logoUrl?: string | null;
  bannerUrl?: string | null;
  supportPhone?: string | null;
  supportEmail?: string | null;
  countryCode: string;
  categoryId: string;
  correspondentPartnerId?: string | null;
  isActive: boolean;
  serviceCount: number;
}

export interface CatalogBillerServiceItem {
  serviceId: string;
  serviceCode: string;
  name: string;
  type: string;
  currency: string;
  minAmount?: number | null;
  maxAmount?: number | null;
  supportsPartialPayment: boolean;
  requiresValidation: boolean;
  isActive: boolean;
  // Spec 040: pricing + import provenance for the admin services drawer.
  amountType?: string | null;
  fixedAmount?: number | null;
  providerItemCode?: string | null;
  customerFieldLabel?: string | null;
}

export interface CatalogBillerServiceResponse {
  services: CatalogBillerServiceItem[];
}

// ── Partner biller catalogue import (Spec 040) ──────────────────────────────
export interface BillerImportSourceItem {
  connectorId: string;
  connectorType: string;
  status: string;
  isSandbox: boolean;
}

export interface BillerImportSourcesResponse {
  sources: BillerImportSourceItem[];
}

export interface BillerImportPreviewRequest {
  connectorId: string;
  categoryCode?: string | null;
  country?: string | null;
}

export type BillerImportStatus = 'New' | 'Mapped' | 'Changed';

export interface BillerImportPreviewEntry {
  billerCode: string;
  billerName: string;
  categoryCode: string;
  categoryName: string;
  serviceCategory: string;
  serviceCount: number;
  importStatus: BillerImportStatus;
  changeNote?: string | null;
}

export interface BillerImportPreviewResponse {
  entries: BillerImportPreviewEntry[];
}

export interface BillerImportSelector {
  billerCode: string;
  itemCodes?: string[] | null;
}

export interface BillerImportRequest {
  connectorId: string;
  entries: BillerImportSelector[];
}

export interface BillerImportSummaryResponse {
  billersCreated: number;
  billersUpdated: number;
  servicesCreated: number;
  servicesUpdated: number;
  deactivated: number;
}

export interface CatalogServiceFieldOption {
  value: string;
  label: string;
}

export interface CatalogServiceField {
  key: string;
  label: string;
  fieldType: string;
  required: boolean;
  minLength?: number | null;
  maxLength?: number | null;
  mask?: string | null;
  placeholder?: string | null;
  options?: CatalogServiceFieldOption[] | null;
}

export interface CatalogServiceValidation {
  validationEndpoint?: string | null;
  validationMode?: string | null;
}

export interface CatalogBillerServiceDetailResponse {
  serviceId: string;
  serviceCode: string;
  name: string;
  type: string;
  currency: string;
  minAmount?: number | null;
  maxAmount?: number | null;
  supportsPartialPayment: boolean;
  requiresValidation: boolean;
  fields: CatalogServiceField[];
  validation?: CatalogServiceValidation | null;
}

export interface CatalogServiceFieldValidationRequest {
  fieldValues: Record<string, string>;
}

export interface CatalogServiceFieldValidationResponse {
  isValid: boolean;
  validatedAt: string;
  errorCode?: string | null;
  errorMessage?: string | null;
  accountHolderName?: string | null;
  additionalInfo?: Record<string, string> | null;
}

export interface CreateBillPaymentOrderRequest {
  payerPartyId: string;
  originCountry: string;
  originCurrency: string;
  purposeCode?: string | null;
  notes?: string | null;
  items?: CreateBillPaymentItemRequest[] | null;
}

export interface CreateBillPaymentItemRequest {
  billerId: string;
  serviceId: string;
  serviceCode: string;
  serviceFieldValues: Record<string, string>;
  receiverPartyId?: string | null;
  newReceiver?: CreateReceiverRequest | null;
  relationshipTypeCode?: string | null;
  originAmount?: number | null;
  destinationAmount?: number | null;
  destinationCurrency: string;
  destinationCountry: string;
  pricingQuoteId: string;
  purposeCode?: string | null;
  notes?: string | null;
}

export interface CreateReceiverRequest {
  displayName: string;
  partyType: string;
  firstName?: string | null;
  lastName?: string | null;
  phone?: string | null;
  email?: string | null;
  countryCode?: string | null;
}

export interface UpdateBillPaymentItemRequest {
  serviceFieldValues?: Record<string, string> | null;
  receiverPartyId?: string | null;
  relationshipTypeCode?: string | null;
  originAmount?: number | null;
  destinationAmount?: number | null;
  pricingQuoteId?: string | null;
  purposeCode?: string | null;
  notes?: string | null;
}

export interface CancelOrderRequest {
  reason?: string | null;
}

export interface OrderItemResponse {
  orderItemId: string;
  itemIndex: number;
  itemType: string;
  status: string;
  billerId: string;
  billerName: string;
  serviceId: string;
  serviceCode: string;
  serviceName: string;
  serviceFieldValues: Record<string, string>;
  receiverPartyId: string;
  receiverName: string;
  relationshipTypeCode?: string | null;
  amountIn: number;
  currencyIn: string;
  amountOut: number;
  currencyOut: string;
  feesTotal: number;
  exchangeRate: number;
  pricingQuoteId?: string | null;
  quoteExpiresAt?: string | null;
  isQuoteExpired: boolean;
}

export interface BillPaymentOrderResponse {
  orderId: string;
  orderType: string;
  status: string;
  payerPartyId: string;
  payerName: string;
  originCountry: string;
  originCurrency: string;
  totalAmountIn: number;
  totalFeesAmount: number;
  totalAmountOut: number;
  destinationCurrency?: string | null;
  purposeCode?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  items: OrderItemResponse[];
}

export interface PricingQuoteRequest {
  originCurrency: string;
  destinationCurrency: string;
  originCountry: string;
  destinationCountry: string;
  serviceCode: string;
  destinationAmount?: number | null;
  originAmount?: number | null;
  customerId?: string | null;
  customerTier?: string | null;
  quoteContext?: string | null;
}

export interface FeeBreakdownItem {
  code: string;
  description: string;
  amount: number;
  currency: string;
  calculationType: string;
}

export interface PricingQuoteResponse {
  pricingQuoteId: string;
  exchangeRate: number;
  rateMarkup: number;
  feesTotal: number;
  totalAmount: number;
  originAmount: number;
  destinationAmount: number;
  pricingPolicyId: string;
  pricingPolicyVersion: string;
  fxRateId: string;
  rateTimestamp: string;
  feeBreakdown: FeeBreakdownItem[];
}

export interface FxQuoteListResponse {
  id: string;
  baseCurrency: string;
  targetCurrency: string;
  rate: number;
  expiresAt: string;
  provider?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface FxQuoteDetailResponse {
  id: string;
  tenantId: string;
  baseCurrency: string;
  targetCurrency: string;
  rate: number;
  expiresAt: string;
  provider?: string | null;
  metadataJson: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateFxQuoteRequest {
  baseCurrency: string;
  targetCurrency: string;
  rate: number;
  expiresAt: string;
  provider?: string | null;
  metadataJson?: string | null;
}

export interface UpdateFxQuoteRequest {
  rate: number;
  expiresAt: string;
  provider?: string | null;
  metadataJson?: string | null;
}

export interface CurrentUserResponse {
  userId: string;
  tenantId: string;
  email?: string | null;
  phone?: string | null;
  status: string;
  partyId?: string | null;
  displayName?: string | null;
}

export interface CreatePartyRequest {
  displayName: string;
  partyType: string;
  firstName?: string | null;
  lastName?: string | null;
  phone?: string | null;
  email?: string | null;
  countryCode?: string | null;
}

export interface CreateCustomerContactRequest {
  type: 'Email' | 'Phone';
  value: string;
  isPrimary: boolean;
}

export interface CreateCustomerAddressRequest {
  type: string;
  line1: string;
  line2?: string | null;
  line3?: string | null;
  city: string;
  state?: string | null;
  postcode: string;
  country: string;
}

export interface CreateCustomerRequest {
  displayName: string;
  partyType: 'Person' | 'Business';
  status: string;
  customerTierCode?: string | null;
  title?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  dob?: string | null;
  nationality?: string | null;
  occupation?: string | null;
  countryCode?: string | null;
  registrationNumber?: string | null;
  incorporationCountry?: string | null;
  industry?: string | null;
  contacts: CreateCustomerContactRequest[];
  addresses: CreateCustomerAddressRequest[];
}

export interface CreateCustomerResponse {
  partyId: string;
  displayName: string;
  partyType: string;
  status: string;
  createdAt: string;
}

export interface PartyResponse {
  partyId: string;
  displayName: string;
  partyType: string;
  status: string;
}

export interface CustomerSummary {
  partyId: string;
  displayName: string;
  partyType: string;
  status: string;
  customerTierCode?: string | null;
  primaryEmail?: string | null;
  primaryPhone?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  photoUrlTiny?: string | null;
  idvStatus?: string | null;
  registrationNumber?: string | null;
  industry?: string | null;
  kybStatus?: string | null;
  countryCode?: string | null;
  createdAt: string;
}

/** An amount in one currency on a registry row — never summed across currencies. */
export interface CustomerRegistryCurrencyTotal {
  currency: string;
  amount: number;
}

export interface CustomerListItem {
  partyId: string;
  displayName: string;
  partyType: string;
  status: string;
  primaryEmail?: string | null;
  primaryPhone?: string | null;
  photoUrlTiny?: string | null;
  verificationStatus?: string | null;
  createdAt: string;
  /** Spec 080 registry columns. Absent on an older server — the page hides those
   * columns entirely rather than rendering placeholder zeros. */
  country?: string | null;
  domains?: string[];
  orderCount?: number;
  totalValue?: CustomerRegistryCurrencyTotal[];
}

/** The product lines that actually have customers in this tenant (Spec 080). */
export interface CustomerRegistryDomainsResponse {
  domains: string[];
}

export interface PartyConsentDetail {
  consentId: string;
  consentType: string;
  grantedAt: string;
  revokedAt?: string | null;
}

export interface PartyAccountDetail {
  partyAccountId: string;
  accountType: string;
  maskedIdentifier: string;
  providerRef?: string | null;
  verificationStatus: string;
  metadataJson: string;
}

export interface PartyRoleAssignmentDetail {
  roleAssignmentId: string;
  role: string;
  contextType: string;
  contextId: string;
}

export interface PartyRelationshipDetail {
  relationshipId: string;
  fromPartyId: string;
  toPartyId: string;
  relationshipTypeCode: string;
  isActive: boolean;
  notes?: string | null;
}

export interface CurrencyAmount {
  currency: string;
  amount: number;
}

export interface CustomerStats {
  partyId: string;
  totalOrders: number;
  /** Lifetime captured payments grouped by currency. */
  totalPaidByCurrency: CurrencyAmount[];
  /** Sum of AmountIn on non-terminal orders, grouped by currency. */
  outstandingByCurrency: CurrencyAmount[];
  lastActivityAt?: string | null;
  /** Count of orders not yet in a terminal status. */
  openOrderCount: number;
  /** Captured payments in the trailing 12 months — closest analogue to ARR. */
  trailingTwelveMonthsByCurrency: CurrencyAmount[];
  /** Captured payments in the trailing 30 days — rough monthly run rate. */
  trailingThirtyDaysByCurrency: CurrencyAmount[];
}

export interface CustomerDetail {
  partyId: string;
  userId?: string | null;
  displayName: string;
  partyType: string;
  status: string;
  customerTierCode?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  personProfile?: PersonProfileDetail | null;
  businessProfile?: BusinessProfileDetail | null;
  contacts: PartyContactDetail[];
  addresses: PartyAddressDetail[];
  consents: PartyConsentDetail[];
  externalAccounts: PartyAccountDetail[];
  roleAssignments: PartyRoleAssignmentDetail[];
  relationships: PartyRelationshipDetail[];
}

export interface UserInfoResponse {
  userId: string;
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  roles: string[];
  tenantId: string;
  partyId: string;
  photoUrl?: string | null;
  photoUrlSmall?: string | null;
  photoUrlTiny?: string | null;
}

export interface RoleSummaryResponse {
  roleId: string;
  name: string;
}

export interface UserRoleResponse {
  userId: string;
  roles: RoleSummaryResponse[];
}

export interface AccessUserSummary {
  userId: string;
  email: string;
  displayName?: string | null;
  status: string;
  lastLoginAt?: string | null;
  roleCount: number;
  partyId?: string | null;
  partyDisplayName?: string | null;
  partyType?: string | null;
  partyLinkType?: string | null;
  photoUrl?: string | null;
  photoUrlSmall?: string | null;
  photoUrlTiny?: string | null;
}

export interface PersonProfileDetail {
  title?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  countryCode?: string | null;
  photoUrl?: string | null;
  photoUrlMedium?: string | null;
  photoUrlSmall?: string | null;
  photoUrlTiny?: string | null;
  dob?: string | null;
  nationality?: string | null;
  occupation?: string | null;
  idvStatus: string;
}

export interface BusinessProfileDetail {
  registrationNumber?: string | null;
  incorporationCountry?: string | null;
  industry?: string | null;
  kybStatus: string;
}

export interface PartyContactDetail {
  contactId: string;
  type: string;
  value: string;
  isPrimary: boolean;
}

export interface PartyAddressDetail {
  addressId: string;
  type: string;
  line1: string;
  line2?: string | null;
  line3?: string | null;
  city: string;
  state?: string | null;
  postcode: string;
  country: string;
}

export interface AccessUserDetail {
  userId: string;
  email: string;
  displayName?: string | null;
  status: string;
  createdAt?: string | null;
  lastLoginAt?: string | null;
  roles: RoleSummaryResponse[];
  permissions?: string[] | null;
  partyId?: string | null;
  partyDisplayName?: string | null;
  partyType?: string | null;
  partyLinkType?: string | null;
  personProfile?: PersonProfileDetail | null;
  businessProfile?: BusinessProfileDetail | null;
  contacts?: PartyContactDetail[];
  addresses?: PartyAddressDetail[];
}

export interface UpdateUserProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  title?: string | null;
  countryCode?: string | null;
  nationality?: string | null;
  occupation?: string | null;
}

export interface UserDiagnosticResult {
  userId: string;
  hasIssues: boolean;
  issues: UserDiagnosticIssue[];
}

export interface UserDiagnosticIssue {
  code: string;
  description: string;
  repairable: boolean;
}

export interface UserRepairResult {
  userId: string;
  repairsApplied: string[];
}

export interface PermissionDefinition {
  key: string;
  description?: string | null;
  category: string;
}

export interface AccessRoleSummary {
  roleId: string;
  name: string;
  description?: string | null;
  permissionCount: number;
  userCount: number;
}

export interface AccessRoleDetail {
  roleId: string;
  name: string;
  description?: string | null;
  permissions: PermissionDefinition[];
  users: AccessUserSummary[];
}

export interface CreateRoleRequest {
  name: string;
  description?: string | null;
  permissionKeys: string[];
}

export interface UpdateRoleRequest {
  name?: string;
  description?: string | null;
  permissionKeys?: string[];
}

export interface InviteUserRequest {
  email: string;
  roleIds?: string[] | null;
  displayName?: string | null;
  /**
   * Optional. When supplied, the placeholder user is linked to this
   * existing Party (which must belong to the current tenant) instead
   * of provisioning a new Individual party. Drives the "invite a
   * customer's contact person as a user" flow.
   */
  partyId?: string | null;
}

// ── Spec 026 · User lifecycle closure ────────────────────────────────────

/// Response from /admin/users/invite and /admin/users/{id}/resend-invite.
/// `emailSent` reports whether the platform notification stack accepted
/// the message — when false, the placeholder + token still exist but
/// the operator should investigate (template missing, mail provider
/// outage, etc.).
export interface InviteUserResponse {
  userId: string;
  tenantId: string;
  email: string;
  displayName?: string | null;
  assignedRoleIds: string[];
  emailSent: boolean;
  expiresUtc?: string | null;
  emailSendCount: number;
}

export interface ResendInviteResponse {
  userId: string;
  email: string;
  emailSent: boolean;
  expiresUtc?: string | null;
  emailSendCount: number;
  rateLimitReason?: string | null;
}

// ── Messaging health ─────────────────────────────────────────────────────

/// Per-channel snapshot returned by /admin/messaging/health. When
/// `configured` is false, `reason` carries a short operator-readable
/// explanation suitable for surfacing in the UI.
export interface MessagingChannelHealth {
  configured: boolean;
  provider: string;
  reason?: string | null;
}

export interface MessagingHealth {
  email: MessagingChannelHealth;
  sms: MessagingChannelHealth;
}

// ── Communication provider settings ──────────────────────────────────────

/// Snapshot returned by GET /admin/settings/communication-provider.
/// Email and SMS are independent channels — each carries its own active
/// provider plus per-provider credential blocks. Secrets are reported
/// via `has*` flags rather than round-tripped.
export interface CommunicationProviderSettingsResponse {
  email: EmailChannelSettingsResponse;
  sms: SmsChannelSettingsResponse;
}

export interface EmailChannelSettingsResponse {
  activeProvider: string;
  azureCommunicationServices?: AzureEmailSettingsResponse | null;
  // Future: sendGrid?: SendGridEmailSettingsResponse | null;
}

export interface SmsChannelSettingsResponse {
  activeProvider: string;
  azureCommunicationServices?: AzureSmsSettingsResponse | null;
  // Future: twilio?: TwilioSmsSettingsResponse | null;
}

export interface AzureEmailSettingsResponse {
  hasConnectionString: boolean;
  fromAddress?: string | null;
}

export interface AzureSmsSettingsResponse {
  hasConnectionString: boolean;
  fromPhoneNumber?: string | null;
}

export interface CommunicationProviderSettingsUpdateRequest {
  email?: EmailChannelSettingsUpdateRequest | null;
  sms?: SmsChannelSettingsUpdateRequest | null;
}

export interface EmailChannelSettingsUpdateRequest {
  activeProvider: string;
  azureCommunicationServices?: AzureEmailSettingsUpdateRequest | null;
}

export interface SmsChannelSettingsUpdateRequest {
  activeProvider: string;
  azureCommunicationServices?: AzureSmsSettingsUpdateRequest | null;
}

export interface AzureEmailSettingsUpdateRequest {
  connectionString?: string | null;
  fromAddress?: string | null;
}

export interface AzureSmsSettingsUpdateRequest {
  connectionString?: string | null;
  fromPhoneNumber?: string | null;
}

export interface SendCommunicationTestRequest {
  channel: 'Email' | 'SMS';
  recipient: string;
  subject?: string | null;
  body?: string | null;
}

export interface SendCommunicationTestResponse {
  sent: boolean;
  channel: string;
  provider: string;
  errorMessage?: string | null;
}

export interface RevokeUserSessionsRequest {
  reason?: string | null;
}

export interface RevokeUserSessionsResponse {
  userId: string;
  revokedUtc: string;
  expiresUtc: string;
  reason: string;
}

export interface DeleteUserRequest {
  emailConfirmation: string;
  reason: string;
}

export interface DeleteUserResponse {
  tombstoneId: string;
  originalUserId: string;
  deletedUtc: string;
  auditRowsRedacted: number;
  identityProviderUserDeleted: boolean;
}

export interface UserTombstoneSummary {
  tombstoneId: string;
  originalUserId: string;
  deletedUtc: string;
  deletedByUserId?: string | null;
  deletedByEmail?: string | null;
  reason: string;
  maskedEmail?: string | null;
  auditRowsRedacted: number;
}

export interface UpdateUserRolesRequest {
  roleIds: string[];
}

// Per-user tenant lookup (authenticated, /host/me/tenants). Carries the
// minimum needed to render the post-auth org picker.
export interface MyTenantSummary {
  tenantId: string;
  name: string;
  subdomain?: string | null;
  environment: TenantEnvironment;
}

export interface MyTenantsResponse {
  tenants: MyTenantSummary[];
}

export interface TenantFeatureItemResponse {
  featureName: string;
  isEnabled: boolean;
  updatedAt?: string | null;
}

export interface TenantFeatureListResponse {
  tenantId: string;
  features: TenantFeatureItemResponse[];
}

export interface TenantFeatureToggleRequest {
  featureName: string;
  isEnabled: boolean;
  reason?: string | null;
}

export interface TenantFeatureUpdateRequest {
  features: TenantFeatureToggleRequest[];
}

// Per-tenant module enablement (Spec 097). Mirrors the
// GET/PUT /admin/tenants/{tenantId}/modules wire contract exactly.
export type TenantModuleSource = 'core' | 'default' | 'pack' | 'explicit';

export interface TenantModuleItemResponse {
  moduleId: string;
  name: string;
  description: string;
  isCore: boolean;
  dependsOn: string[];
  softDependsOn: string[];
  isEnabled: boolean;
  source: TenantModuleSource;
  reason?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface TenantModuleListResponse {
  tenantId: string;
  modules: TenantModuleItemResponse[];
}

export interface TenantModuleToggleRequest {
  moduleId: string;
  isEnabled: boolean;
  reason?: string | null;
}

export interface TenantModuleUpdateRequest {
  modules: TenantModuleToggleRequest[];
}

/** 409 body returned when a toggle violates the module dependency graph. */
export interface ModuleDependencyErrorBody {
  error: string;
  code: 'module.dependency_missing' | 'module.dependents_enabled';
  moduleId: string;
  relatedModuleIds: string[];
}

/** 403 body returned by the module gate when a disabled module is called. */
export interface ModuleDisabledErrorBody {
  error: string;
  code: 'module.disabled';
  moduleId: string;
}

export type DemoSeedType = 'BillCollection' | 'CrossBorderPayments';

export interface DemoSeedResponse {
  tenantId: string;
  seedType: DemoSeedType;
  seededAt: string;
  operations: string[];
}

export interface PermissionSeedResponse {
  tenantId: string;
  seededAt: string;
  operations: string[];
}

export interface CacheSetSummary {
  name: string;
  entryCount: number;
}

export interface CacheOverviewResponse {
  cacheSets: CacheSetSummary[];
  totalCacheSets: number;
  totalEntries: number;
}

export interface InvalidateCacheSetResponse {
  cacheSet: string;
  invalidated: boolean;
  invalidatedAtUtc: string;
}

export interface DataSeedInfo {
  key: string;
  displayName: string;
  description: string;
  sortOrder: number;
}

export interface DataSeedAvailableResponse {
  seeds: DataSeedInfo[];
}

export interface DataSeedResultItem {
  key: string;
  displayName: string;
  operations: string[];
}

export interface DataSeedResponse {
  seededAt: string;
  results: DataSeedResultItem[];
}

// Spec 029 — Keycloak added as a third operator-choice provider.
export type AuthProviderType = 'AzureAd' | 'Auth0' | 'Keycloak';

export interface Auth0SettingsResponse {
  domain?: string | null;
  audience?: string | null;
  clientId?: string | null;
  managementClientId?: string | null;
  hasManagementClientSecret: boolean;
  connection?: string | null;
  managementAudience?: string | null;
}

export interface AzureAdSettingsResponse {
  authority?: string | null;
  audience?: string | null;
  clientId?: string | null;
  hasClientSecret: boolean;
  tenantId?: string | null;
  userPrincipalNameDomain?: string | null;
}

export interface KeycloakSettingsResponse {
  authority?: string | null;
  audience?: string | null;
  clientId?: string | null;
  hasClientSecret: boolean;
  realm?: string | null;
  adminClientId?: string | null;
  hasAdminClientSecret: boolean;
}

export interface AuthProviderSettingsResponse {
  activeProvider: AuthProviderType;
  auth0: Auth0SettingsResponse;
  azureAd: AzureAdSettingsResponse;
  keycloak: KeycloakSettingsResponse;
}

export interface Auth0SettingsUpdateRequest {
  domain?: string | null;
  audience?: string | null;
  clientId?: string | null;
  managementClientId?: string | null;
  managementClientSecret?: string | null;
  connection?: string | null;
  managementAudience?: string | null;
}

export interface AzureAdSettingsUpdateRequest {
  authority?: string | null;
  audience?: string | null;
  clientId?: string | null;
  clientSecret?: string | null;
  tenantId?: string | null;
  userPrincipalNameDomain?: string | null;
}

export interface KeycloakSettingsUpdateRequest {
  authority?: string | null;
  audience?: string | null;
  clientId?: string | null;
  clientSecret?: string | null;
  realm?: string | null;
  adminClientId?: string | null;
  adminClientSecret?: string | null;
}

export interface AuthProviderSettingsUpdateRequest {
  activeProvider: AuthProviderType;
  auth0?: Auth0SettingsUpdateRequest | null;
  azureAd?: AzureAdSettingsUpdateRequest | null;
  keycloak?: KeycloakSettingsUpdateRequest | null;
}

export interface PaymentGatewayProviderResponse {
  providerCode: string;
  enabled: boolean;
  baseUrl: string;
  idpTokenUrl: string;
  clientId: string;
  defaultTransferPurpose: string;
  hasClientSecret: boolean;
  hasEncryptionKey: boolean;
  hasSigningSecret: boolean;
  secretSource: 'Database' | 'Configuration' | 'None' | string;
}

export interface PaymentGatewaySettingsResponse {
  providers: PaymentGatewayProviderResponse[];
}

export interface PaymentGatewayProviderUpdateRequest {
  providerCode: string;
  enabled: boolean;
  baseUrl: string;
  idpTokenUrl: string;
  clientId: string;
  defaultTransferPurpose: string;
  clientSecret?: string | null;
  encryptionKey?: string | null;
  signingSecret?: string | null;
}

export interface PaymentGatewaySettingsUpdateRequest {
  providers: PaymentGatewayProviderUpdateRequest[];
}

export interface TestPaymentGatewayRequest {
  providerCode: string;
}

export interface TestPaymentGatewayResponse {
  succeeded: boolean;
  providerCode: string;
  errorMessage?: string | null;
}

export interface TextToSpeechCredentialResponse {
  provider: string;
  hasHostCredential: boolean;
  hasTenantOverride: boolean;
  effectiveSource: string;
}

export interface TextToSpeechCredentialUpdateRequest {
  provider: string;
  apiKey?: string | null;
  clearStoredValue: boolean;
}

// Ledger Types
export interface LedgerSummary {
  id: string;
  baseCurrency: string;
  createdUtc: string;
}

export interface CreateLedgerRequest {
  baseCurrency: string;
}

export interface LedgerAccountSummary {
  id: string;
  ledgerId: string;
  name: string;
  code: string;
  accountType: string;
  currency: string;
  createdUtc: string;
  /** Running balance per currency. Empty when the account has no posted lines. */
  balancesByCurrency: LedgerAccountBalance[];
}

export interface LedgerAccountBalance {
  currency: string;
  balance: number;
}

export interface CreateLedgerAccountRequest {
  ledgerId: string;
  name: string;
  code: string;
  accountType: string;
}

export interface JournalEntryLineRequest {
  accountId: string;
  direction: string;
  amount: number;
  currency: string;
  narration?: string | null;
}

export interface AddJournalEntryRequest {
  ledgerId: string;
  reference?: string | null;
  description?: string | null;
  lines: JournalEntryLineRequest[];
}

export interface JournalEntryLineResponse {
  id: string;
  accountId: string;
  direction: string;
  amount: number;
  currency: string;
  narration?: string | null;
}

export interface JournalEntryResponse {
  id: string;
  ledgerId: string;
  entryUtc: string;
  status: string;
  reference?: string | null;
  description?: string | null;
  lines: JournalEntryLineResponse[];
}

// Autonumbering Types
export type AutonumberStrategy = 'Sequential' | 'Random' | 'Hybrid';
export type AutonumberResetPolicy = 'None' | 'Monthly' | 'Yearly';

export interface AutonumberProfile {
  id: string;
  tenantId: string;
  entityType: string;
  prefixTemplate: string;
  suffixTemplate: string;
  strategy: AutonumberStrategy;
  resetPolicy: AutonumberResetPolicy;
  paddingLength: number;
  minValue: number;
  maxValue: number;
  lastIssuedValue: number;
  lastIssuedAt?: string | null;
  isActive: boolean;
}

export interface UpsertAutonumberProfileRequest {
  entityType: string;
  prefixTemplate?: string | null;
  suffixTemplate?: string | null;
  strategy: AutonumberStrategy;
  resetPolicy: AutonumberResetPolicy;
  paddingLength: number;
  minValue: number;
  maxValue: number;
  isActive: boolean;
}

export interface GenerateAutonumberRequest {
  entityType: string;
  tenantId?: string | null;
}

export interface GenerateAutonumberResponse {
  profileId: string;
  sequenceValue: number;
  reference: string;
}

// ── Account Connections (Tenant-Scoped Bank Linking) ────
export interface AccountConnectionResponse {
  connectionId: string;
  provider: string;
  providerDisplayName: string;
  institutionName: string;
  institutionReference?: string | null;
  status: string;
  consentStatus: string;
  autoSyncEnabled: boolean;
  lastSyncedAt?: string | null;
  lastSyncStatus?: string | null;
  lastError?: string | null;
  disconnectedAt?: string | null;
  linkedAccounts: LinkedAccountResponse[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface LinkedAccountResponse {
  linkedAccountId: string;
  accountId: string;
  name: string;
  accountType: string;
  accountSubtype?: string | null;
  currency: string;
  last4?: string | null;
  status: string;
  lastSyncedAt?: string | null;
  lastSyncStatus?: string | null;
  lastError?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AccountLinkSessionResponse {
  sessionId: string;
  provider: string;
  providerDisplayName: string;
  mode: string;
  status: string;
  connectionId?: string | null;
  launchToken: string;
  expiresAt: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AccountLinkExchangeResponse {
  sessionId: string;
  connection: AccountConnectionResponse;
}

export interface AccountTransactionResponse {
  transactionId: string;
  accountId: string;
  accountConnectionId: string | null;
  occurredAt: string;
  amount: number;
  currency: string;
  counterparty?: string | null;
  description?: string | null;
  reference?: string | null;
  category?: string | null;
  pending: boolean;
  reconciliationStatus: string;
  matchedLedgerEntryId?: string | null;
  matchedPayoutId?: string | null;
  reconciledAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AccountTransactionSyncResponse {
  connectionId: string;
  transactionsAdded: number;
  transactionsUpdated: number;
  transactionsRemoved: number;
  transactionsSkipped: number;
  syncStatus: string;
  nextCursor?: string | null;
  syncedAt: string;
}

// ── Manual Account CRUD ─────────────────────────────────
export interface AccountResponse {
  accountId: string;
  accountType: string;
  maskedIdentifier: string;
  providerRef?: string | null;
  verificationStatus: string;
  currency?: string | null;
  country?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateAccountRequest {
  name: string;
  accountType: string;
  currency: string;
  country?: string | null;
  institutionName?: string | null;
  last4?: string | null;
  notes?: string | null;
}

export interface CreateAccountTransactionRequest {
  accountId: string;
  occurredAt: string;
  amount: number;
  currency: string;
  counterparty?: string | null;
  description?: string | null;
  reference?: string | null;
  category?: string | null;
  notes?: string | null;
}

export interface AccountTransactionAttachmentResponse {
  attachmentId: string;
  fileName: string;
  contentType: string;
  url: string;
  fileSizeBytes: number;
  createdAt: string;
}

// ── Notification Templates ───────────────────────────────────────────────────

export interface NotificationTemplateResponse {
  id: string;
  tenantId: string | null;
  name: string;
  channel: string;
  subjectTemplate: string;
  bodyTemplate: string;
  description: string;
  isShared: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface NotificationTemplateSummary {
  id: string;
  name: string;
  channel: string;
  description: string;
  isShared: boolean;
  isActive: boolean;
}

export interface CreateNotificationTemplateRequest {
  name: string;
  channel: string;
  subjectTemplate: string;
  bodyTemplate: string;
  description: string;
  isShared: boolean;
  isActive: boolean;
}

export interface UpdateNotificationTemplateRequest {
  subjectTemplate: string;
  bodyTemplate: string;
  description: string;
  isShared: boolean;
  isActive: boolean;
}

export interface PreviewNotificationTemplateRequest {
  subjectTemplate: string;
  bodyTemplate: string;
  sampleModelJson: string;
}

export interface PreviewNotificationTemplateResponse {
  subject: string;
  body: string;
}

export interface NotificationTemplateBindingResponse {
  id: string;
  tenantId: string;
  templateName: string;
  channel: string;
  baseTemplateId: string | null;
  overrideTemplateId: string | null;
  isEnabled: boolean;
}

export interface CreateNotificationTemplateBindingRequest {
  templateName: string;
  channel: string;
  baseTemplateId: string | null;
  overrideTemplateId: string | null;
  isEnabled: boolean;
}

export interface UpdateNotificationTemplateBindingRequest {
  baseTemplateId: string | null;
  overrideTemplateId: string | null;
  isEnabled: boolean;
}

// ── Billing / Invoices ──────────────────────────────────────────────

export interface InvoiceResponse {
  id: string;
  /** CustomerAccount FK on the invoice. Prefer customerPartyId for display. */
  customerId: string;
  invoiceNumber: string;
  currency: string;
  totalAmount: number;
  status: string;
  issuedUtc: string;
  dueUtc: string;
  lineItems: InvoiceLineItemResponse[];
  /** Party.Id resolved via Invoice.CustomerAccountId → CustomerAccount.CustomerPartyId. */
  customerPartyId?: string | null;
  /** Party.DisplayName. Empty string when the party row couldn't be resolved. */
  customerName?: string;
}

export interface InvoiceLineItemResponse {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface CreateInvoiceRequest {
  customerId: string;
  invoiceNumber: string;
  currency: string;
  dueUtc: string;
  lineItems: CreateInvoiceLineItemRequest[];
}

export interface CreateInvoiceLineItemRequest {
  description: string;
  quantity: number;
  unitPrice: number;
}

// ── Personal Finance — Accounts ─────────────────────────────────────

export interface PersonalAccountResponse {
  personalAccountId: string;
  userId: string;
  householdId?: string | null;
  name: string;
  accountType: string;
  currency: string;
  institutionName?: string | null;
  externalReference?: string | null;
  status: string;
  accountSubtype?: string | null;
  last4?: string | null;
  currentBalance: number;
  balanceAsOf?: string | null;
  isArchived: boolean;
  openedAt?: string | null;
  closedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreatePersonalAccountRequest {
  name: string;
  accountType: string;
  currency: string;
  institutionName?: string | null;
  externalReference?: string | null;
  accountSubtype?: string | null;
  last4?: string | null;
  startingBalance?: number | null;
}

// ── Personal Finance — Transactions ─────────────────────────────────

export interface PersonalTransactionResponse {
  personalTransactionId: string;
  userId: string;
  personalAccountId?: string | null;
  financialContextId?: string | null;
  sourceType: string;
  occurredAt: string;
  amount: number;
  currency: string;
  transactionType: string;
  merchant?: string | null;
  description?: string | null;
  category?: string | null;
  subCategory?: string | null;
  confidence: number;
  categorisedBy?: string | null;
  classificationMethod?: string | null;
  notes?: string | null;
  tags: string[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateManualPersonalTransactionRequest {
  personalAccountId?: string | null;
  occurredAt: string;
  amount: number;
  currency: string;
  merchant?: string | null;
  description?: string | null;
  category?: string | null;
  notes?: string | null;
  tags?: string[] | null;
}

// ── Personal Finance — Categories ────────────────────────────────────

export interface TransactionCategoryGroupResponse {
  groupName: string;
  categories: TransactionCategoryResponse[];
}

export interface TransactionCategoryResponse {
  code: string;
  displayName: string;
  groupName: string;
  iconName?: string | null;
  sortOrder: number;
  subCategories?: TransactionSubCategoryResponse[] | null;
}

export interface TransactionSubCategoryResponse {
  code: string;
  displayName: string;
  iconName?: string | null;
  sortOrder: number;
}

export interface TransactionCategoryListResponse {
  groups: TransactionCategoryGroupResponse[];
  categories: TransactionCategoryResponse[];
}

// ── Personal Finance — Admin Budget ─────────────────────────────────────

export interface AdminBudgetLineItem {
  category: string;
  limitAmount: number;
  currency: string;
}

export interface AdminBudgetResponse {
  budgetId: string;
  periodType: string;
  periodStart: string;
  status: string;
  lines: AdminBudgetLineItem[];
}

// ── Personal Finance — Commitments ──────────────────────────────────────

export interface CommitmentItem {
  commitmentId: string;
  commitmentType: string;
  verificationStatus: string;
  origin: string;
  displayName: string;
  amount?: number | null;
  currency: string;
  dueDate: string;
  frequency: string;
  status: string;
  autopay: boolean;
  paidFromAccountId?: string | null;
  category?: string | null;
  confidenceScore?: number | null;
  lastPaidAt?: string | null;
  lastPaidAmount?: number | null;
  createdAt: string;
}

export interface CommitmentTotals {
  totalUpcomingAmount: number;
  dueSoonCount: number;
  detectedCount: number;
  billsCount: number;
  subscriptionsCount: number;
  debtRepaymentsCount: number;
}

export interface CommitmentListResponse {
  items: CommitmentItem[];
  page: number;
  pageSize: number;
  hasMore: boolean;
  totals: CommitmentTotals;
}

// ── CustomerDetail — extended with userId ───────────────────────────────

export interface CustomerDetailWithUserId {
  userId?: string | null;
}
