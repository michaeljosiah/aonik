import { api } from '@/lib/api';
import type { PricingQuoteRequest, PricingQuoteResponse } from '@/types';

export const pricingService = {
  getQuote: async (request: PricingQuoteRequest): Promise<PricingQuoteResponse> => {
    return api.post<PricingQuoteResponse>('/pricing/quote', request);
  },
};
