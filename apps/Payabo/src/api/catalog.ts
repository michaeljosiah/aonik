import { apiGet, apiPost } from "./client";

type CatalogCountryResponse = {
  countries: Array<{ countryCode: string; name: string }>;
};

type CatalogBillerCategoryResponse = {
  categories: Array<{
    categoryId: string;
    name: string;
    description: string | null;
    iconUrl: string | null;
    countryCode: string;
  }>;
};

type CatalogBillerResponse = {
  billers: Array<{
    billerId: string;
    name: string;
    logoUrl: string | null;
    countryCode: string;
    categoryId: string;
    correspondentPartnerId: string;
    isActive: boolean;
    isFeatured: boolean;
  }>;
  pagination: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
};

type CatalogBillerServiceResponse = {
  services: Array<{
    serviceId: string;
    serviceCode: string;
    name: string;
    type: string;
    currency: string;
    minAmount: number | null;
    maxAmount: number | null;
    supportsPartialPayment: boolean;
    requiresValidation: boolean;
    isActive: boolean;
  }>;
};

type CatalogBillerServiceDetailResponse = {
  serviceId: string;
  serviceCode: string;
  name: string;
  type: string;
  currency: string;
  minAmount: number | null;
  maxAmount: number | null;
  supportsPartialPayment: boolean;
  requiresValidation: boolean;
  fields: Array<{
    key: string;
    label: string;
    fieldType: string;
    required: boolean;
    minLength: number | null;
    maxLength: number | null;
    mask: string | null;
    placeholder: string | null;
    options: Array<{ value: string; label: string }> | null;
  }>;
  validation: {
    validationEndpoint: string | null;
    validationMode: string | null;
  } | null;
};

type CatalogServiceFieldValidationResponse = {
  isValid: boolean;
  validatedAt: string;
  errorCode: string | null;
  errorMessage: string | null;
  accountHolderName: string | null;
  additionalInfo: Record<string, string> | null;
};

export type CatalogCountry = {
  code: string;
  name: string;
};

export type PublicCatalogCountryQuery = {
  onlyServiceCountries?: boolean;
  capabilityType?: string;
};

export type CatalogBillerCategory = {
  id: string;
  name: string;
  description: string | null;
  iconUrl: string | null;
  countryCode: string;
};

export type PublicCatalogBillerCategoryQuery = {
  countryCode?: string;
};

export type CatalogBiller = {
  id: string;
  name: string;
  logoUrl: string | null;
  countryCode: string;
  categoryId: string;
  isFeatured: boolean;
};

export type CatalogPagination = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type PublicCatalogBillerQuery = {
  countryCode?: string;
  categoryId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
};

export type CatalogBillerService = {
  id: string;
  code: string;
  name: string;
  type: string;
  currency: string;
  minAmount: number | null;
  maxAmount: number | null;
  supportsPartialPayment: boolean;
  requiresValidation: boolean;
};

export type CatalogServiceFieldOption = {
  value: string;
  label: string;
};

export type CatalogServiceField = {
  key: string;
  label: string;
  fieldType: string;
  required: boolean;
  minLength: number | null;
  maxLength: number | null;
  mask: string | null;
  placeholder: string | null;
  options: CatalogServiceFieldOption[];
};

export type CatalogServiceValidation = {
  validationEndpoint: string | null;
  validationMode: string | null;
};

export type CatalogBillerServiceDetail = {
  id: string;
  code: string;
  name: string;
  type: string;
  currency: string;
  minAmount: number | null;
  maxAmount: number | null;
  supportsPartialPayment: boolean;
  requiresValidation: boolean;
  fields: CatalogServiceField[];
  validation: CatalogServiceValidation | null;
};

export type CatalogServiceFieldValidationResult = {
  isValid: boolean;
  errorCode: string | null;
  errorMessage: string | null;
  accountHolderName: string | null;
  additionalInfo: Record<string, string> | null;
};

export const getPublicCatalogCountries = async (
  options: PublicCatalogCountryQuery = {}
): Promise<CatalogCountry[]> => {
  const params = new URLSearchParams();

  if (options.onlyServiceCountries !== undefined) {
    params.set("onlyServiceCountries", String(options.onlyServiceCountries));
  }

  if (options.capabilityType) {
    params.set("capabilityType", options.capabilityType);
  }

  const query = params.toString();
  const path = query ? `/public/catalog/countries?${query}` : "/public/catalog/countries";
  const response = await apiGet<CatalogCountryResponse>(path);

  return response.countries.map((country) => ({
    code: country.countryCode,
    name: country.name
  }));
};

export const getPublicCatalogBillerCategories = async (
  options: PublicCatalogBillerCategoryQuery = {}
): Promise<CatalogBillerCategory[]> => {
  const params = new URLSearchParams();

  if (options.countryCode) {
    params.set("countryCode", options.countryCode);
  }

  const query = params.toString();
  const path = query ? `/public/catalog/billers/categories?${query}` : "/public/catalog/billers/categories";
  const response = await apiGet<CatalogBillerCategoryResponse>(path);

  return response.categories.map((category) => ({
    id: category.categoryId,
    name: category.name,
    description: category.description,
    iconUrl: category.iconUrl,
    countryCode: category.countryCode
  }));
};

export const getPublicCatalogBillers = async (
  options: PublicCatalogBillerQuery
): Promise<{ billers: CatalogBiller[]; pagination: CatalogPagination }> => {
  const params = new URLSearchParams();

  if (options.countryCode) {
    params.set("countryCode", options.countryCode);
  }

  if (options.categoryId) {
    params.set("categoryId", options.categoryId);
  }

  if (options.search) {
    params.set("search", options.search);
  }

  params.set("page", String(options.page ?? 1));
  params.set("pageSize", String(options.pageSize ?? 12));

  const response = await apiGet<CatalogBillerResponse>(`/public/catalog/billers?${params.toString()}`);

  return {
    billers: response.billers.map((biller) => ({
      id: biller.billerId,
      name: biller.name,
      logoUrl: biller.logoUrl,
      countryCode: biller.countryCode,
      categoryId: biller.categoryId,
      isFeatured: biller.isFeatured
    })),
    pagination: {
      page: response.pagination.page,
      pageSize: response.pagination.pageSize,
      totalCount: response.pagination.totalCount,
      totalPages: response.pagination.totalPages
    }
  };
};

export const getPublicCatalogBillerServices = async (billerId: string): Promise<CatalogBillerService[]> => {
  const response = await apiGet<CatalogBillerServiceResponse>(`/public/catalog/billers/${billerId}/services`);

  return response.services.map((service) => ({
    id: service.serviceId,
    code: service.serviceCode,
    name: service.name,
    type: service.type,
    currency: service.currency,
    minAmount: service.minAmount,
    maxAmount: service.maxAmount,
    supportsPartialPayment: service.supportsPartialPayment,
    requiresValidation: service.requiresValidation
  }));
};

export const getPublicCatalogBillerServiceDetail = async (
  billerId: string,
  serviceId: string
): Promise<CatalogBillerServiceDetail> => {
  const response = await apiGet<CatalogBillerServiceDetailResponse>(`/public/catalog/billers/${billerId}/services/${serviceId}`);

  return {
    id: response.serviceId,
    code: response.serviceCode,
    name: response.name,
    type: response.type,
    currency: response.currency,
    minAmount: response.minAmount,
    maxAmount: response.maxAmount,
    supportsPartialPayment: response.supportsPartialPayment,
    requiresValidation: response.requiresValidation,
    fields: response.fields.map((field) => ({
      key: field.key,
      label: field.label,
      fieldType: field.fieldType,
      required: field.required,
      minLength: field.minLength,
      maxLength: field.maxLength,
      mask: field.mask,
      placeholder: field.placeholder,
      options: field.options ?? []
    })),
    validation: response.validation
      ? {
          validationEndpoint: response.validation.validationEndpoint,
          validationMode: response.validation.validationMode
        }
      : null
  };
};

export const validatePublicCatalogServiceFields = async (
  billerId: string,
  serviceId: string,
  fieldValues: Record<string, string>
): Promise<CatalogServiceFieldValidationResult> => {
  const response = await apiPost<CatalogServiceFieldValidationResponse>(
    `/public/catalog/billers/${billerId}/services/${serviceId}/validate`,
    { fieldValues }
  );

  return {
    isValid: response.isValid,
    errorCode: response.errorCode,
    errorMessage: response.errorMessage,
    accountHolderName: response.accountHolderName,
    additionalInfo: response.additionalInfo
  };
};
