import { api } from '@/lib/api';
import type { MySpaceSummaryResponse } from '@/types';

export const mySpaceService = {
  /**
   * Fetches the dashboard summary. When `currency` is provided and is in the
   * tenant's configured currency set, the cash timeline is built in that
   * currency; otherwise the tenant's primary settlement currency is used.
   */
  getSummary: async (currency?: string): Promise<MySpaceSummaryResponse> => {
    const path = currency
      ? `/insights/myspace-summary?currency=${encodeURIComponent(currency)}`
      : '/insights/myspace-summary';
    return api.get<MySpaceSummaryResponse>(path);
  },
};
