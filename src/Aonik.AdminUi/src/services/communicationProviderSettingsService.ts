import { api } from '@/lib/api';
import type {
  CommunicationProviderSettingsResponse,
  CommunicationProviderSettingsUpdateRequest,
  SendCommunicationTestRequest,
  SendCommunicationTestResponse,
} from '@/types';

/**
 * Backs the SettingsCommunicationPage. Secrets are write-only: leave
 * secret fields empty to keep existing provider credentials.
 */
export const communicationProviderSettingsService = {
  get: async (): Promise<CommunicationProviderSettingsResponse> => {
    return api.get<CommunicationProviderSettingsResponse>('/admin/settings/communication-provider');
  },

  update: async (
    request: CommunicationProviderSettingsUpdateRequest,
  ): Promise<CommunicationProviderSettingsResponse> => {
    return api.put<CommunicationProviderSettingsResponse>(
      '/admin/settings/communication-provider',
      request,
    );
  },

  /**
   * Fires a one-off test email or SMS via the active provider so the
   * operator can verify their configuration without running a full
   * invite or registration flow. The backend returns 200 even on
   * delivery failure — `sent: false` + `errorMessage` lets the UI
   * render the failure inline.
   */
  sendTest: async (
    request: SendCommunicationTestRequest,
  ): Promise<SendCommunicationTestResponse> => {
    return api.post<SendCommunicationTestResponse>(
      '/admin/settings/communication-provider/test-send',
      request,
    );
  },
};
