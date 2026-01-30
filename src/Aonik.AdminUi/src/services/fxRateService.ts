import { api } from '@/lib/api';
import type {
  FxQuoteListResponse,
  FxQuoteDetailResponse,
  CreateFxQuoteRequest,
  UpdateFxQuoteRequest,
} from '@/types';

export const fxRateService = {
  getAll: async (params?: {
    baseCurrency?: string;
    targetCurrency?: string;
    includeExpired?: boolean;
  }): Promise<FxQuoteListResponse[]> => {
    const queryParams = new URLSearchParams();
    if (params?.baseCurrency) queryParams.append('baseCurrency', params.baseCurrency);
    if (params?.targetCurrency) queryParams.append('targetCurrency', params.targetCurrency);
    if (params?.includeExpired !== undefined)
      queryParams.append('includeExpired', params.includeExpired.toString());

    const url = `/fx-quotes${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;
    return api.get<FxQuoteListResponse[]>(url);
  },

  getById: async (id: string): Promise<FxQuoteDetailResponse> => {
    return api.get<FxQuoteDetailResponse>(`/fx-quotes/${id}`);
  },

  create: async (request: CreateFxQuoteRequest): Promise<FxQuoteDetailResponse> => {
    return api.post<FxQuoteDetailResponse>('/fx-quotes', request);
  },

  update: async (id: string, request: UpdateFxQuoteRequest): Promise<FxQuoteDetailResponse> => {
    return api.put<FxQuoteDetailResponse>(`/fx-quotes/${id}`, request);
  },

  delete: async (id: string): Promise<void> => {
    return api.delete(`/fx-quotes/${id}`);
  },
};
