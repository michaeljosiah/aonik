import { api } from '@/lib/api';
import type {
  CatalogCountryResponse,
  CatalogBillerCategoryResponse,
  CatalogBillerResponse,
  CatalogBillerDetailResponse,
  CatalogBillerServiceResponse,
  CatalogBillerServiceDetailResponse,
} from '@/types';

export interface CatalogBillerListParams {
  countryCode?: string;
  categoryId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export const catalogService = {
  getCountries: async (onlyServiceCountries = false): Promise<CatalogCountryResponse> => {
    const params = new URLSearchParams();
    if (onlyServiceCountries) {
      params.append('onlyServiceCountries', 'true');
    }

    const query = params.toString();
    return api.get<CatalogCountryResponse>(`/host/catalog/countries${query ? `?${query}` : ''}`);
  },

  getCategories: async (countryCode?: string): Promise<CatalogBillerCategoryResponse> => {
    const params = new URLSearchParams();
    if (countryCode) {
      params.append('countryCode', countryCode);
    }

    const query = params.toString();
    return api.get<CatalogBillerCategoryResponse>(`/host/catalog/billers/categories${query ? `?${query}` : ''}`);
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

  getBillerDetail: async (billerId: string): Promise<CatalogBillerDetailResponse> => {
    return api.get<CatalogBillerDetailResponse>(`/host/catalog/billers/${billerId}`);
  },

  getBillerServices: async (billerId: string): Promise<CatalogBillerServiceResponse> => {
    return api.get<CatalogBillerServiceResponse>(`/host/catalog/billers/${billerId}/services`);
  },

  getBillerServiceDetail: async (billerId: string, serviceId: string): Promise<CatalogBillerServiceDetailResponse> => {
    return api.get<CatalogBillerServiceDetailResponse>(
      `/host/catalog/billers/${billerId}/services/${serviceId}`
    );
  },
};
