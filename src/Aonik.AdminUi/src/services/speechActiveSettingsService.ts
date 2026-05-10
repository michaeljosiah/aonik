import { api } from '@/lib/api';
import type {
  ChatSpeechSettings,
  UpdateChatSpeechSettingsRequest,
  UpdateVoiceModeSettingsRequest,
  VoiceModeSettings,
} from '@/types/speechActiveSettings';

/** Voice Mode active settings — singleton per tenant. */
export const voiceModeSettingsService = {
  get: async (): Promise<VoiceModeSettings> =>
    api.get<VoiceModeSettings>('/tenant/voice-mode-settings'),

  update: async (request: UpdateVoiceModeSettingsRequest): Promise<VoiceModeSettings> =>
    api.put<VoiceModeSettings>('/tenant/voice-mode-settings', request),
};

/** Chat Speech active settings — singleton per tenant. */
export const chatSpeechSettingsService = {
  get: async (): Promise<ChatSpeechSettings> =>
    api.get<ChatSpeechSettings>('/tenant/chat-speech-settings'),

  update: async (request: UpdateChatSpeechSettingsRequest): Promise<ChatSpeechSettings> =>
    api.put<ChatSpeechSettings>('/tenant/chat-speech-settings', request),
};
