import { api } from '@/lib/api';
import type {
  CreateVoiceRecipeRequest,
  UpdateVoiceRecipeRequest,
  VoiceRecipe,
  VoiceRecipeHistoryEntry,
  VoiceRecipeKind,
  VoiceRecipeStatus,
} from '@/types/voiceRecipes';

/** Typed client for the voice recipe library backend (spec 024 Phase B). */
export const voiceRecipeLibraryService = {
  list: async (
    options: { kind?: VoiceRecipeKind; includeDisabled?: boolean } = {},
  ): Promise<VoiceRecipe[]> => {
    return api.get<VoiceRecipe[]>('/tenant/voice-recipes', {
      params: {
        kind: options.kind,
        includeDisabled: options.includeDisabled ?? false,
      },
    });
  },

  get: async (id: string): Promise<VoiceRecipe | null> => {
    try {
      return await api.get<VoiceRecipe>(`/tenant/voice-recipes/${encodeURIComponent(id)}`);
    } catch (err) {
      if ((err as { response?: { status?: number } })?.response?.status === 404) return null;
      throw err;
    }
  },

  create: async (request: CreateVoiceRecipeRequest): Promise<VoiceRecipe> => {
    return api.post<VoiceRecipe>('/tenant/voice-recipes', request);
  },

  update: async (id: string, request: UpdateVoiceRecipeRequest): Promise<VoiceRecipe> => {
    return api.put<VoiceRecipe>(`/tenant/voice-recipes/${encodeURIComponent(id)}`, request);
  },

  setStatus: async (id: string, status: VoiceRecipeStatus): Promise<VoiceRecipe> => {
    return api.put<VoiceRecipe>(
      `/tenant/voice-recipes/${encodeURIComponent(id)}/status`,
      { status },
    );
  },

  getHistory: async (id: string): Promise<VoiceRecipeHistoryEntry[]> => {
    return api.get<VoiceRecipeHistoryEntry[]>(
      `/tenant/voice-recipes/${encodeURIComponent(id)}/history`,
    );
  },
};
