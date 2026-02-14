import { api } from '@/lib/api';
import type { CacheOverviewResponse, InvalidateCacheSetResponse } from '@/types';

export const cacheManagementService = {
  getOverview: async (): Promise<CacheOverviewResponse> => {
    return api.get<CacheOverviewResponse>('/admin/cache');
  },

  invalidateCacheSet: async (cacheSet: string): Promise<InvalidateCacheSetResponse> => {
    return api.post<InvalidateCacheSetResponse>('/admin/cache/invalidate', { cacheSet });
  },
};
