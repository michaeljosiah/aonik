import { api } from '@/lib/api';
import type { CreateCustomerRequest, CreateCustomerResponse, CustomerDetail, CustomerListItem, PagedResult } from '@/types';

export interface ListCustomersParams {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  partyType?: string;
  search?: string;
}

export const customerService = {
  list: async (params: ListCustomersParams = {}): Promise<PagedResult<CustomerListItem>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.status) queryParams.append('status', params.status);
    if (params.partyType) queryParams.append('partyType', params.partyType);
    if (params.search) queryParams.append('search', params.search);

    const query = queryParams.toString();
    return api.get<PagedResult<CustomerListItem>>(`/admin/customers${query ? `?${query}` : ''}`);
  },
  get: async (partyId: string): Promise<CustomerDetail> => {
    return api.get<CustomerDetail>(`/admin/customers/${partyId}`);
  },
  create: async (data: CreateCustomerRequest): Promise<CreateCustomerResponse> => {
    return api.post<CreateCustomerResponse>('/admin/customers', data);
  },
};
