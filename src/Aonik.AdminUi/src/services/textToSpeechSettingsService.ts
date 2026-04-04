import { api } from '@/lib/api';
import apiClient from '@/lib/api';
import type { AxiosError } from 'axios';
import type {
  TextToSpeechCredentialResponse,
  TextToSpeechCredentialUpdateRequest,
  TextToSpeechPreviewRequest,
  TextToSpeechSettingsResponse,
  TextToSpeechSettingsUpdateRequest,
  TextToSpeechVoiceOptionResponse,
} from '@/types';

export interface TextToSpeechPreviewAudioResponse {
  audioBlob: Blob;
  contentType: string;
  provider: string | null;
  voiceId: string | null;
  aiRunId: string | null;
}

function tryGetString(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function tryGetNestedErrorMessage(data: unknown): string | null {
  if (!data || typeof data !== 'object') {
    return null;
  }

  const errors = (data as { errors?: unknown }).errors;
  if (!errors || typeof errors !== 'object') {
    return null;
  }

  const generalErrors = (errors as { generalErrors?: unknown }).generalErrors;
  if (Array.isArray(generalErrors)) {
    const firstGeneralError = generalErrors.map(tryGetString).find(Boolean);
    if (firstGeneralError) {
      return firstGeneralError;
    }
  }

  for (const value of Object.values(errors as Record<string, unknown>)) {
    if (!Array.isArray(value)) {
      continue;
    }

    const firstMessage = value.map(tryGetString).find(Boolean);
    if (firstMessage) {
      return firstMessage;
    }
  }

  return null;
}

async function resolvePreviewErrorMessage(error: unknown): Promise<string | null> {
  if (!error || typeof error !== 'object') {
    return null;
  }

  const response = (error as AxiosError).response;
  const data = response?.data;
  if (!(data instanceof Blob)) {
    return null;
  }

  try {
    const text = await data.text();
    if (!text) {
      return null;
    }

    const parsed = JSON.parse(text) as unknown;
    return tryGetNestedErrorMessage(parsed)
      ?? tryGetString((parsed as { message?: unknown } | null)?.message)
      ?? tryGetString((parsed as { error?: unknown } | null)?.error)
      ?? tryGetString(text);
  } catch {
    return null;
  }
}

export const textToSpeechSettingsService = {
  get: async (): Promise<TextToSpeechSettingsResponse> => {
    return api.get<TextToSpeechSettingsResponse>('/tenant/settings/text-to-speech');
  },

  update: async (request: TextToSpeechSettingsUpdateRequest): Promise<TextToSpeechSettingsResponse> => {
    return api.put<TextToSpeechSettingsResponse>('/tenant/settings/text-to-speech', request);
  },

  getHostCredential: async (): Promise<TextToSpeechCredentialResponse> => {
    return api.get<TextToSpeechCredentialResponse>('/admin/settings/text-to-speech/credentials/host');
  },

  updateHostCredential: async (request: TextToSpeechCredentialUpdateRequest): Promise<TextToSpeechCredentialResponse> => {
    return api.put<TextToSpeechCredentialResponse>('/admin/settings/text-to-speech/credentials/host', request);
  },

  getTenantCredential: async (): Promise<TextToSpeechCredentialResponse> => {
    return api.get<TextToSpeechCredentialResponse>('/tenant/settings/text-to-speech/credentials');
  },

  updateTenantCredential: async (request: TextToSpeechCredentialUpdateRequest): Promise<TextToSpeechCredentialResponse> => {
    return api.put<TextToSpeechCredentialResponse>('/tenant/settings/text-to-speech/credentials', request);
  },

  listVoices: async (provider?: string): Promise<TextToSpeechVoiceOptionResponse[]> => {
    return api.get<TextToSpeechVoiceOptionResponse[]>('/tenant/settings/text-to-speech/voices', {
      params: provider ? { provider } : undefined,
    });
  },

  preview: async (request: TextToSpeechPreviewRequest): Promise<TextToSpeechPreviewAudioResponse> => {
    try {
      const response = await apiClient.post('/tenant/settings/text-to-speech/preview', request, {
        responseType: 'blob',
      });

      return {
        audioBlob: response.data,
        contentType: response.headers['content-type'] ?? 'audio/mpeg',
        provider: response.headers['x-tts-provider'] ?? null,
        voiceId: response.headers['x-tts-voice-id'] ?? null,
        aiRunId: response.headers['x-ai-run-id'] ?? null,
      };
    } catch (error: unknown) {
      const userMessage = await resolvePreviewErrorMessage(error);
      if (userMessage) {
        if (error && typeof error === 'object') {
          throw { ...error, userMessage };
        }

        throw { userMessage };
      }

      throw error;
    }
  },
};
