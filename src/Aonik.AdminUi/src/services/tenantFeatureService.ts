import { api } from '@/lib/api';
import type { TenantFeatureListResponse, TenantFeatureUpdateRequest } from '@/types';

export const tenantFeatureService = {
  get: async (tenantId: string): Promise<TenantFeatureListResponse> => {
    return api.get<TenantFeatureListResponse>(`/admin/tenants/${tenantId}/features`);
  },
  update: async (tenantId: string, request: TenantFeatureUpdateRequest): Promise<TenantFeatureListResponse> => {
    return api.put<TenantFeatureListResponse>(`/admin/tenants/${tenantId}/features`, request);
  },
};
