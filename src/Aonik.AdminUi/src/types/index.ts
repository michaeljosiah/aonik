// Navigation Types
export interface NavItem {
  id: string;
  label: string;
  icon: string;
  href?: string;
  badge?: string;
  children?: NavItem[];
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
}

export interface UpdateTenantRequest {
  name?: string;
  environment?: TenantEnvironment;
  defaultCurrency?: string;
  supportedCountries?: string[];
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
