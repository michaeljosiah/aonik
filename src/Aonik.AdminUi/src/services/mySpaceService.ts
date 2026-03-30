import { api } from '@/lib/api';
import type { MySpaceSummaryResponse } from '@/types';

export const mySpaceService = {
  getSummary: async (): Promise<MySpaceSummaryResponse> => {
    return api.get<MySpaceSummaryResponse>('/insights/myspace-summary');
  },
};
