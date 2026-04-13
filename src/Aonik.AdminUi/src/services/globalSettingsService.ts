import { api } from '@/lib/api';

export interface SettingValueResponse {
  key: string;
  value: string | null;
  source: string;
}

export interface BatchGetSettingValuesResponse {
  settings: SettingValueResponse[];
}

export const globalSettingsService = {
  /**
   * Fetch multiple global setting values in a single request.
   */
  batchGet: async (keys: string[]): Promise<BatchGetSettingValuesResponse> => {
    return api.post<BatchGetSettingValuesResponse>('/admin/settings/values/batch', { keys });
  },

  /**
   * Get a single global setting value.
   */
  get: async (key: string): Promise<SettingValueResponse> => {
    return api.get<SettingValueResponse>(`/admin/settings/values/${encodeURIComponent(key)}`);
  },

  /**
   * Update a single global setting value.
   */
  update: async (key: string, value: string | null): Promise<SettingValueResponse> => {
    return api.put<SettingValueResponse>(`/admin/settings/values/${encodeURIComponent(key)}`, {
      key,
      value,
    });
  },
};
