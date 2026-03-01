import { api } from '@/lib/api';
import type { AuthProviderSettingsResponse, AuthProviderSettingsUpdateRequest } from '@/types';

export const authProviderSettingsService = {
  get: async (): Promise<AuthProviderSettingsResponse> => {
    return api.get<AuthProviderSettingsResponse>('/admin/settings/auth-provider');
  },

  update: async (request: AuthProviderSettingsUpdateRequest): Promise<AuthProviderSettingsResponse> => {
    return api.put<AuthProviderSettingsResponse>('/admin/settings/auth-provider', request);
  },
};
