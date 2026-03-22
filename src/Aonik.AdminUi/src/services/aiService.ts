import { api } from '@/lib/api';
import type {
  AiProviderResponse,
  CreateAiProviderRequest,
  UpdateAiProviderRequest,
  AiModelResponse,
  CreateAiModelRequest,
  UpdateAiModelRequest,
  AgentConfigurationResponse,
  UpsertAgentConfigurationRequest,
  ListAiProvidersResponse,
  ListAiModelsResponse,
} from '@/types/ai';

// ── Provider service ────────────────────────────────────────────────

export const aiProviderService = {
  list: async (): Promise<AiProviderResponse[]> => {
    const res = await api.get<ListAiProvidersResponse>('/ai/providers');
    return res.providers;
  },

  get: async (providerId: string): Promise<AiProviderResponse> => {
    return api.get<AiProviderResponse>(`/ai/providers/${providerId}`);
  },

  create: async (request: CreateAiProviderRequest): Promise<AiProviderResponse> => {
    return api.post<AiProviderResponse>('/ai/providers', request);
  },

  update: async (providerId: string, request: UpdateAiProviderRequest): Promise<AiProviderResponse> => {
    return api.put<AiProviderResponse>(`/ai/providers/${providerId}`, request);
  },

  delete: async (providerId: string): Promise<void> => {
    await api.delete(`/ai/providers/${providerId}`);
  },
};

// ── Model service ───────────────────────────────────────────────────

export const aiModelService = {
  list: async (providerId?: string): Promise<AiModelResponse[]> => {
    const query = providerId ? `?providerId=${providerId}` : '';
    const res = await api.get<ListAiModelsResponse>(`/ai/models${query}`);
    return res.models;
  },

  get: async (modelId: string): Promise<AiModelResponse> => {
    return api.get<AiModelResponse>(`/ai/models/${modelId}`);
  },

  create: async (request: CreateAiModelRequest): Promise<AiModelResponse> => {
    return api.post<AiModelResponse>('/ai/models', request);
  },

  update: async (modelId: string, request: UpdateAiModelRequest): Promise<AiModelResponse> => {
    return api.put<AiModelResponse>(`/ai/models/${modelId}`, request);
  },

  delete: async (modelId: string): Promise<void> => {
    await api.delete(`/ai/models/${modelId}`);
  },
};

// ── Agent configuration service ─────────────────────────────────────

interface ListAgentConfigurationsResponse {
  configurations: AgentConfigurationResponse[];
}

export const agentConfigService = {
  list: async (): Promise<AgentConfigurationResponse[]> => {
    const res = await api.get<ListAgentConfigurationsResponse>('/ai/agents/configurations');
    return res.configurations;
  },

  get: async (agentName: string): Promise<AgentConfigurationResponse> => {
    return api.get<AgentConfigurationResponse>(`/ai/agents/configurations/${agentName}`);
  },

  upsert: async (agentName: string, request: UpsertAgentConfigurationRequest): Promise<AgentConfigurationResponse> => {
    return api.put<AgentConfigurationResponse>(`/ai/agents/configurations/${agentName}`, request);
  },

  delete: async (agentName: string): Promise<void> => {
    await api.delete(`/ai/agents/configurations/${agentName}`);
  },
};
