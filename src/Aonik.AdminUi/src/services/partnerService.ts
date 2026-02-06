import { api } from '@/lib/api';
import type { PagedResult } from '@/types';
import type {
  CreatePartnerRequest,
  CreatePartnerResponse,
  PartnerDetail,
  PartnerListItem,
  UpdatePartnerRequest,
} from '@/types/partners';

export interface ListPartnersParams {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  countryCode?: string;
  search?: string;
}

export const partnerService = {
  list: async (params: ListPartnersParams = {}): Promise<PagedResult<PartnerListItem>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.status) queryParams.append('status', params.status);
    if (params.countryCode) queryParams.append('countryCode', params.countryCode);
    if (params.search) queryParams.append('search', params.search);

    const query = queryParams.toString();
    return api.get<PagedResult<PartnerListItem>>(`/admin/partners${query ? `?${query}` : ''}`);
  },

  get: async (partnerId: string): Promise<PartnerDetail> => {
    return api.get<PartnerDetail>(`/admin/partners/${partnerId}`);
  },

  create: async (request: CreatePartnerRequest): Promise<CreatePartnerResponse> => {
    return api.post<CreatePartnerResponse>('/admin/partners', request);
  },

  update: async (partnerId: string, request: UpdatePartnerRequest): Promise<PartnerDetail> => {
    return api.patch<PartnerDetail>(`/admin/partners/${partnerId}`, request);
  },

  delete: async (partnerId: string): Promise<void> => {
    await api.delete(`/admin/partners/${partnerId}`);
  },
};
