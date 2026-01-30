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
  supportedCurrencies: string[];
  status: TenantStatus;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateTenantRequest {
  name: string;
  environment: TenantEnvironment;
  defaultCurrency: string;
  supportedCountries: string[];
  supportedCurrencies?: string[];
}

export interface UpdateTenantRequest {
  name?: string;
  environment?: TenantEnvironment;
  defaultCurrency?: string;
  supportedCountries?: string[];
  supportedCurrencies?: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface BootstrapTenantResult {
  tenantId: string;
  tenantName: string;
  tenantCreated: boolean;
  userId: string;
  userCreated: boolean;
  tenantAdminAssigned: boolean;
}

export interface BootstrapStatusResponse {
  platformAdminEmailsConfigured: boolean;
  isCurrentUserAllowed: boolean;
  tenantCount: number;
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
}

export interface CatalogBillerServiceResponse {
  services: CatalogBillerServiceItem[];
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

export interface PartyResponse {
  partyId: string;
  displayName: string;
  partyType: string;
  status: string;
}

export interface UserInfoResponse {
  userId: string;
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  roles: string[];
  tenantId: string;
  partyId: string;
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
}

export interface UpdateUserRolesRequest {
  roleIds: string[];
}

// Tenant list for login dropdown (public endpoint)
export interface TenantListItemForLogin {
  tenantId: string;
  name: string;
  subdomain?: string | null;
  environment: TenantEnvironment;
}

export interface TenantListForLoginResponse {
  tenants: TenantListItemForLogin[];
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

export interface DemoSeedResponse {
  tenantId: string;
  seededAt: string;
  operations: string[];
}
