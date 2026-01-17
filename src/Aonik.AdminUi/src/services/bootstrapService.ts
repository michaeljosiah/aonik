import { api } from '@/lib/api';
import type { BootstrapStatusResponse, BootstrapTenantResult } from '@/types';

export const bootstrapService = {
  bootstrap: async (): Promise<BootstrapTenantResult> => {
    return api.post<BootstrapTenantResult>('/bootstrap');
  },
  status: async (): Promise<BootstrapStatusResponse> => {
    return api.get<BootstrapStatusResponse>('/bootstrap/status');
  },
};
