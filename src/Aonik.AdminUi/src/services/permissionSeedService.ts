import { api } from '@/lib/api';
import type { PermissionSeedResponse } from '@/types';

export const permissionSeedService = {
  seed: async (tenantId: string): Promise<PermissionSeedResponse> => {
    return api.post<PermissionSeedResponse>(`/admin/tenants/${tenantId}/permissions/seed`);
  },
};
