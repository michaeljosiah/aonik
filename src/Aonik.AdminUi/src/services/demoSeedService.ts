import { api } from '@/lib/api';
import type { DemoSeedResponse } from '@/types';

export const demoSeedService = {
  seed: async (tenantId: string): Promise<DemoSeedResponse> => {
    return api.post<DemoSeedResponse>(`/admin/tenants/${tenantId}/demo-seed`);
  },
};
