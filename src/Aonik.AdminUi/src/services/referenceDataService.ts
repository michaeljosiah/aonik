import { api } from '@/lib/api';
import type { ReferenceDataItem } from '@/types';

export const referenceDataService = {
  getItems: async (type: string): Promise<ReferenceDataItem[]> => {
    return api.get<ReferenceDataItem[]>(`/reference-data/${encodeURIComponent(type)}`);
  },
};
