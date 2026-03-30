import { api } from '@/lib/api';
import type { DataSeedAvailableResponse, DataSeedResponse } from '@/types';

export const dataSeedService = {
  list: async (): Promise<DataSeedAvailableResponse> => {
    return api.get<DataSeedAvailableResponse>('/admin/data-seeds');
  },

  run: async (keys?: string[]): Promise<DataSeedResponse> => {
    return api.post<DataSeedResponse>('/admin/data-seeds/run', { keys: keys ?? null });
  },
};
