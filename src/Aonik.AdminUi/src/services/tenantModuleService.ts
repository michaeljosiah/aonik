import { api } from '@/lib/api';
import type { TenantModuleListResponse, TenantModuleUpdateRequest } from '@/types';

// Per-tenant module enablement (Spec 097). Reading is open to platform and
// tenant admins; writing is host-only — the server enforces both.
export const tenantModuleService = {
  get: async (tenantId: string): Promise<TenantModuleListResponse> => {
    return api.get<TenantModuleListResponse>(`/admin/tenants/${tenantId}/modules`);
  },
  update: async (tenantId: string, request: TenantModuleUpdateRequest): Promise<TenantModuleListResponse> => {
    return api.put<TenantModuleListResponse>(`/admin/tenants/${tenantId}/modules`, request);
  },
};
