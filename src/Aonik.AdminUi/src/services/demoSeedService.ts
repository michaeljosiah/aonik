import { api } from '@/lib/api';
import type { DemoSeedResponse, DemoSeedType } from '@/types';

export const demoSeedService = {
  seed: async (tenantId: string, seedType?: DemoSeedType): Promise<DemoSeedResponse> => {
    return api.post<DemoSeedResponse>(
      `/admin/tenants/${tenantId}/demo-seed`,
      seedType ? { seedType } : undefined,
    );
  },

  reverse: async (tenantId: string): Promise<DemoSeedResponse> => {
    return api.delete<DemoSeedResponse>(`/admin/tenants/${tenantId}/demo-seed`);
  },
};
