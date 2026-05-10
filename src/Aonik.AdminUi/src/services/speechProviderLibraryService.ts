import { api } from '@/lib/api';
import apiClient from '@/lib/api';
import type {
  CreateSpeechProviderRequest,
  SetSpeechProviderStatusRequest,
  SpeechProvider,
  SpeechProviderHistoryEntry,
  SpeechProviderType,
  SpeechProviderUsage,
  SpeechVendorsCatalogResponse,
  TestSpeechProviderSttResponse,
  UpdateSpeechProviderRequest,
} from '@/types/speechLibrary';

/**
 * Typed client for the speech library backend (spec 024 Phase A.2).
 * Mirrors the surface of `voiceProviderSettingsService` so the
 * AdminUi can compose the same patterns (Blob audio playback, multipart upload).
 */
export const speechProviderLibraryService = {
  list: async (
    options: { type?: SpeechProviderType; includeDisabled?: boolean } = {},
  ): Promise<SpeechProvider[]> => {
    return api.get<SpeechProvider[]>('/tenant/speech-providers', {
      params: {
        type: options.type,
        includeDisabled: options.includeDisabled ?? false,
      },
    });
  },

  get: async (id: string): Promise<SpeechProvider | null> => {
    try {
      return await api.get<SpeechProvider>(
        `/tenant/speech-providers/${encodeURIComponent(id)}`,
      );
    } catch (err) {
      if ((err as { response?: { status?: number } })?.response?.status === 404) return null;
      throw err;
    }
  },

  create: async (request: CreateSpeechProviderRequest): Promise<SpeechProvider> => {
    return api.post<SpeechProvider>('/tenant/speech-providers', request);
  },

  update: async (id: string, request: UpdateSpeechProviderRequest): Promise<SpeechProvider> => {
    return api.put<SpeechProvider>(
      `/tenant/speech-providers/${encodeURIComponent(id)}`,
      request,
    );
  },

  setStatus: async (
    id: string,
    request: SetSpeechProviderStatusRequest,
  ): Promise<SpeechProvider> => {
    return api.put<SpeechProvider>(
      `/tenant/speech-providers/${encodeURIComponent(id)}/status`,
      request,
    );
  },

  getHistory: async (id: string): Promise<SpeechProviderHistoryEntry[]> => {
    return api.get<SpeechProviderHistoryEntry[]>(
      `/tenant/speech-providers/${encodeURIComponent(id)}/history`,
    );
  },

  getUsage: async (id: string): Promise<SpeechProviderUsage> => {
    return api.get<SpeechProviderUsage>(
      `/tenant/speech-providers/${encodeURIComponent(id)}/usage`,
    );
  },

  /**
   * Synthesize a short clip with the provider's stored configuration. Returns WAV
   * (raw PCM in a 44-byte RIFF/WAVE header) so the browser's native `<audio>` plays
   * it without resampling.
   */
  testTts: async (
    id: string,
    text: string,
    voiceId: string,
    modelId?: string | null,
  ): Promise<{ audioBlob: Blob; sampleRate: number | null }> => {
    const response = await apiClient.post(
      `/tenant/speech-providers/${encodeURIComponent(id)}/test-tts`,
      { text, voiceId, modelId: modelId ?? null },
      { responseType: 'blob' },
    );
    const sampleRateHeader = response.headers['x-voice-sample-rate'] as string | undefined;
    return {
      audioBlob: response.data,
      sampleRate: sampleRateHeader ? Number.parseInt(sampleRateHeader, 10) : null,
    };
  },

  /**
   * Transcribe a short PCM clip captured from the mic. The audio Blob carries 16-bit
   * PCM at the supplied sample rate (default 16 kHz).
   */
  testStt: async (
    id: string,
    audio: Blob,
    sampleRate?: number,
  ): Promise<TestSpeechProviderSttResponse> => {
    const form = new FormData();
    form.append('audio', audio, 'preview.pcm');
    if (sampleRate) form.append('sampleRate', String(sampleRate));
    const response = await apiClient.post<TestSpeechProviderSttResponse>(
      `/tenant/speech-providers/${encodeURIComponent(id)}/test-stt`,
      form,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    );
    return response.data;
  },
};

export const speechVendorsCatalogService = {
  /** Fetch the per-vendor form schema. Cache aggressively in the UI; this is static-ish data. */
  get: async (): Promise<SpeechVendorsCatalogResponse> => {
    return api.get<SpeechVendorsCatalogResponse>('/speech-vendors');
  },
};
