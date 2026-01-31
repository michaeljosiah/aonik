import { api } from '@/lib/api';
import type { BootstrapStatusResponse, BootstrapTenantResult } from '@/types';

// Simple in-memory cache for bootstrap status
let statusCache: { data: BootstrapStatusResponse; timestamp: number } | null = null;
const CACHE_TTL_MS = 30000; // 30 seconds

export const bootstrapService = {
  bootstrap: async (): Promise<BootstrapTenantResult> => {
    // Clear cache after bootstrap
    statusCache = null;
    return api.post<BootstrapTenantResult>('/bootstrap');
  },
  status: async (): Promise<BootstrapStatusResponse> => {
    // Return cached data if valid
    if (statusCache && Date.now() - statusCache.timestamp < CACHE_TTL_MS) {
      return statusCache.data;
    }

    // Fetch fresh data
    const data = await api.get<BootstrapStatusResponse>('/bootstrap/status');
    
    // Update cache
    statusCache = { data, timestamp: Date.now() };
    
    return data;
  },
  clearCache: (): void => {
    statusCache = null;
  },
};
