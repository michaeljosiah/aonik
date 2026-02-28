import { api } from '@/lib/api';
import type { BootstrapStatusResponse, BootstrapTenantResult } from '@/types';

// Simple in-memory cache for bootstrap status
let statusCache: { data: BootstrapStatusResponse; timestamp: number } | null = null;
const CACHE_TTL_MS = 30000; // 30 seconds

export const bootstrapService = {
  bootstrap: async (accessToken?: string | null): Promise<BootstrapTenantResult> => {
    // Clear cache after bootstrap
    statusCache = null;
    const config = accessToken
      ? { headers: { Authorization: `Bearer ${accessToken}` } }
      : undefined;
    return api.post<BootstrapTenantResult>('/bootstrap', undefined, config);
  },
  status: async (forceRefresh = false, accessToken?: string | null): Promise<BootstrapStatusResponse> => {
    // Return cached data if valid
    if (!forceRefresh && statusCache && Date.now() - statusCache.timestamp < CACHE_TTL_MS) {
      return statusCache.data;
    }

    // Fetch fresh data
    const config = accessToken
      ? { headers: { Authorization: `Bearer ${accessToken}` } }
      : undefined;
    const data = await api.get<BootstrapStatusResponse>('/bootstrap/status', config);
    
    // Update cache
    statusCache = { data, timestamp: Date.now() };
    
    return data;
  },
  clearCache: (): void => {
    statusCache = null;
  },
};
