import { api } from '@/lib/api';
import type {
  CatalogCountryResponse,
  CatalogCurrencyResponse,
  CatalogBillerCategoryItem,
  CatalogBillerCategoryResponse,
  CatalogBillerResponse,
  CatalogBillerDetailResponse,
  CatalogBillerServiceResponse,
  CatalogBillerServiceDetailResponse,
  CatalogServiceFieldValidationRequest,
  CatalogServiceFieldValidationResponse,
  CreateCatalogBillerCategoryRequest,
  UpdateCatalogBillerCategoryRequest,
  CreateCatalogBillerRequest,
  UpdateCatalogBillerRequest,
} from '@/types';

export interface CatalogBillerListParams {
  countryCode?: string;
  categoryId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export const catalogService = {
  getCountries: async (onlyServiceCountries = false, capabilityType?: string): Promise<CatalogCountryResponse> => {
    const params = new URLSearchParams();
    if (onlyServiceCountries) {
      params.append('onlyServiceCountries', 'true');
    }
    if (capabilityType) {
      params.append('capabilityType', capabilityType);
    }

    const query = params.toString();
    return api.get<CatalogCountryResponse>(`/host/catalog/countries${query ? `?${query}` : ''}`);
  },

  getCurrencies: async (includeInactive = false, countryCode?: string): Promise<CatalogCurrencyResponse> => {
    const params = new URLSearchParams();
    if (includeInactive) {
      params.append('includeInactive', 'true');
    }
    if (countryCode) {
      params.append('countryCode', countryCode);
    }

    const query = params.toString();
    return api.get<CatalogCurrencyResponse>(`/host/catalog/currencies${query ? `?${query}` : ''}`);
  },

  getTenantCountries: async (onlyServiceCountries = false, capabilityType?: string): Promise<CatalogCountryResponse> => {
    const params = new URLSearchParams();
    if (onlyServiceCountries) {
      params.append('onlyServiceCountries', 'true');
    }
    if (capabilityType) {
      params.append('capabilityType', capabilityType);
    }

    const query = params.toString();
    return api.get<CatalogCountryResponse>(`/catalog/countries${query ? `?${query}` : ''}`);
  },

  getTenantCurrencies: async (includeInactive = false, countryCode?: string): Promise<CatalogCurrencyResponse> => {
    const params = new URLSearchParams();
    if (includeInactive) {
      params.append('includeInactive', 'true');
    }
    if (countryCode) {
      params.append('countryCode', countryCode);
    }

    const query = params.toString();
    return api.get<CatalogCurrencyResponse>(`/catalog/currencies${query ? `?${query}` : ''}`);
  },

  getCategories: async (countryCode?: string): Promise<CatalogBillerCategoryResponse> => {
    const params = new URLSearchParams();
    if (countryCode) {
      params.append('countryCode', countryCode);
    }

    const query = params.toString();
    return api.get<CatalogBillerCategoryResponse>(`/host/catalog/billers/categories${query ? `?${query}` : ''}`);
  },

  getTenantCategories: async (countryCode?: string): Promise<CatalogBillerCategoryResponse> => {
    const params = new URLSearchParams();
    if (countryCode) {
      params.append('countryCode', countryCode);
    }

    const query = params.toString();
    return api.get<CatalogBillerCategoryResponse>(`/catalog/billers/categories${query ? `?${query}` : ''}`);
  },

  getBillers: async (params: CatalogBillerListParams = {}): Promise<CatalogBillerResponse> => {
    const queryParams = new URLSearchParams();
    if (params.countryCode) queryParams.append('countryCode', params.countryCode);
    if (params.categoryId) queryParams.append('categoryId', params.categoryId);
    if (params.search) queryParams.append('search', params.search);
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());

    const query = queryParams.toString();
    return api.get<CatalogBillerResponse>(`/host/catalog/billers${query ? `?${query}` : ''}`);
  },

  getTenantBillers: async (params: CatalogBillerListParams = {}): Promise<CatalogBillerResponse> => {
    const queryParams = new URLSearchParams();
    if (params.countryCode) queryParams.append('countryCode', params.countryCode);
    if (params.categoryId) queryParams.append('categoryId', params.categoryId);
    if (params.search) queryParams.append('search', params.search);
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());

    const query = queryParams.toString();
    return api.get<CatalogBillerResponse>(`/catalog/billers${query ? `?${query}` : ''}`);
  },

  getBillerDetail: async (billerId: string): Promise<CatalogBillerDetailResponse> => {
    return api.get<CatalogBillerDetailResponse>(`/host/catalog/billers/${billerId}`);
  },

  getTenantBillerDetail: async (billerId: string): Promise<CatalogBillerDetailResponse> => {
    return api.get<CatalogBillerDetailResponse>(`/catalog/billers/${billerId}`);
  },

  getBillerServices: async (billerId: string): Promise<CatalogBillerServiceResponse> => {
    return api.get<CatalogBillerServiceResponse>(`/host/catalog/billers/${billerId}/services`);
  },

  getTenantBillerServices: async (billerId: string): Promise<CatalogBillerServiceResponse> => {
    return api.get<CatalogBillerServiceResponse>(`/catalog/billers/${billerId}/services`);
  },

  getBillerServiceDetail: async (billerId: string, serviceId: string): Promise<CatalogBillerServiceDetailResponse> => {
    return api.get<CatalogBillerServiceDetailResponse>(
      `/host/catalog/billers/${billerId}/services/${serviceId}`
    );
  },

  getTenantBillerServiceDetail: async (
    billerId: string,
    serviceId: string
  ): Promise<CatalogBillerServiceDetailResponse> => {
    return api.get<CatalogBillerServiceDetailResponse>(
      `/catalog/billers/${billerId}/services/${serviceId}`
    );
  },

  validateServiceFields: async (
    billerId: string,
    serviceId: string,
    request: CatalogServiceFieldValidationRequest
  ): Promise<CatalogServiceFieldValidationResponse> => {
    return api.post<CatalogServiceFieldValidationResponse>(
      `/catalog/billers/${billerId}/services/${serviceId}/validate`,
      request
    );
  },

  // ── Tenant catalog mutations ────────────────────────────────────────────
  // All routes are tenant-scoped: the API resolves the current tenant from
  // the X-Tenant-Id header. The TenantAdmin role holds Catalog.Write.

  createTenantCategory: async (
    request: CreateCatalogBillerCategoryRequest
  ): Promise<CatalogBillerCategoryItem> => {
    return api.post<CatalogBillerCategoryItem>('/catalog/billers/categories', request);
  },

  updateTenantCategory: async (
    categoryId: string,
    request: UpdateCatalogBillerCategoryRequest
  ): Promise<CatalogBillerCategoryItem> => {
    return api.put<CatalogBillerCategoryItem>(`/catalog/billers/categories/${categoryId}`, request);
  },

  deleteTenantCategory: async (categoryId: string): Promise<void> => {
    return api.delete<void>(`/catalog/billers/categories/${categoryId}`);
  },

  createTenantBiller: async (
    request: CreateCatalogBillerRequest
  ): Promise<CatalogBillerDetailResponse> => {
    return api.post<CatalogBillerDetailResponse>('/catalog/billers', request);
  },

  updateTenantBiller: async (
    billerId: string,
    request: UpdateCatalogBillerRequest
  ): Promise<CatalogBillerDetailResponse> => {
    return api.put<CatalogBillerDetailResponse>(`/catalog/billers/${billerId}`, request);
  },

  deleteTenantBiller: async (billerId: string): Promise<void> => {
    return api.delete<void>(`/catalog/billers/${billerId}`);
  },
};
