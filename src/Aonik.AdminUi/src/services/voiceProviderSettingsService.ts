import { api } from '@/lib/api';
import apiClient from '@/lib/api';
import type {
  SttPreviewRequest,
  SttPreviewResponse,
  VoiceOptionResponse,
  VoicePreviewAudioResponse,
  VoicePreviewRequest,
  VoiceProviderCredentialResponse,
  VoiceProviderCredentialUpdateRequest,
  VoiceProviderSettingsResponse,
  VoiceProviderSettingsUpdateRequest,
  VoiceRecipeResponse,
} from '@/types/voice';

/**
 * Typed client for the Voice & Speech settings backend endpoints. Mirrors
 * `textToSpeechSettingsService` shape so the page can compose the same
 * patterns used by the existing TTS settings page.
 */
export const voiceProviderSettingsService = {
  get: async (): Promise<VoiceProviderSettingsResponse> => {
    return api.get<VoiceProviderSettingsResponse>('/tenant/settings/voice');
  },

  update: async (
    request: VoiceProviderSettingsUpdateRequest,
  ): Promise<VoiceProviderSettingsResponse> => {
    return api.put<VoiceProviderSettingsResponse>('/tenant/settings/voice', request);
  },

  listRecipes: async (): Promise<VoiceRecipeResponse[]> => {
    return api.get<VoiceRecipeResponse[]>('/tenant/settings/voice/recipes');
  },

  listVoices: async (provider: string): Promise<VoiceOptionResponse[]> => {
    return api.get<VoiceOptionResponse[]>('/tenant/settings/voice/voices', {
      params: { provider },
    });
  },

  getCredential: async (provider: string): Promise<VoiceProviderCredentialResponse> => {
    return api.get<VoiceProviderCredentialResponse>(
      `/tenant/settings/voice/credentials/${encodeURIComponent(provider)}`,
    );
  },

  updateCredential: async (
    provider: string,
    request: VoiceProviderCredentialUpdateRequest,
  ): Promise<VoiceProviderCredentialResponse> => {
    return api.put<VoiceProviderCredentialResponse>(
      `/tenant/settings/voice/credentials/${encodeURIComponent(provider)}`,
      request,
    );
  },

  /**
   * Synthesize a TTS preview clip. Server returns WAV audio (raw PCM wrapped in a 44-byte
   * RIFF/WAVE header) so the browser's `<audio>` element can play it without resampling.
   */
  preview: async (request: VoicePreviewRequest): Promise<VoicePreviewAudioResponse> => {
    const response = await apiClient.post('/tenant/settings/voice/preview', request, {
      responseType: 'blob',
    });

    const sampleRateHeader = response.headers['x-voice-sample-rate'] as string | undefined;
    return {
      audioBlob: response.data,
      contentType:
        (response.headers['content-type'] as string | undefined) ?? 'audio/wav',
      provider: (response.headers['x-voice-provider'] as string | undefined) ?? null,
      voiceId: (response.headers['x-voice-id'] as string | undefined) ?? null,
      sampleRate: sampleRateHeader ? Number.parseInt(sampleRateHeader, 10) : null,
    };
  },

  /**
   * Transcribe a captured PCM/WAV clip via the chosen STT provider. Used by the admin "Test STT"
   * card to validate credentials, language, and region against a short mic recording.
   */
  previewStt: async (request: SttPreviewRequest): Promise<SttPreviewResponse> => {
    const form = new FormData();
    form.append('audio', request.audio, 'preview.pcm');
    form.append('provider', request.provider);
    if (request.model) form.append('model', request.model);
    if (request.language) form.append('language', request.language);
    if (request.region) form.append('region', request.region);
    if (request.sampleRate) form.append('sampleRate', String(request.sampleRate));

    const response = await apiClient.post<SttPreviewResponse>(
      '/tenant/settings/voice/preview-stt',
      form,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    );
    return response.data;
  },
};
