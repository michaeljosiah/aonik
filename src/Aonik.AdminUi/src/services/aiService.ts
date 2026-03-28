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
  AgentRunSummary,
  ListAiProvidersResponse,
  ListAiModelsResponse,
  AiCatalogModelProviderResponse,
  AiCatalogModelResponse,
  ImportAiCatalogModelProviderRequest,
  ImportAiCatalogModelProviderResponse,
  ListAiCatalogModelProvidersResponse,
  ListAiCatalogModelsResponse,
} from '@/types/ai';
import type { PagedResult } from '@/types';

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

// ── Model catalog service ────────────────────────────────────────────

export const aiModelCatalogService = {
  listModelProviders: async (): Promise<AiCatalogModelProviderResponse[]> => {
    const res = await api.get<ListAiCatalogModelProvidersResponse>('/ai/model-catalog/model-providers');
    return res.modelProviders;
  },

  listModels: async (modelProviderKey: string): Promise<AiCatalogModelResponse[]> => {
    const encodedModelProviderKey = encodeURIComponent(modelProviderKey);
    const res = await api.get<ListAiCatalogModelsResponse>(`/ai/model-catalog/model-providers/${encodedModelProviderKey}/models`);
    return res.models;
  },

  importModelProvider: async (
    modelProviderKey: string,
    request: ImportAiCatalogModelProviderRequest,
  ): Promise<ImportAiCatalogModelProviderResponse> => {
    const encodedModelProviderKey = encodeURIComponent(modelProviderKey);
    return api.post<ImportAiCatalogModelProviderResponse>(
      `/ai/model-catalog/model-providers/${encodedModelProviderKey}/import`,
      request,
    );
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

// ── Agent run service ────────────────────────────────────────────────

export const agentRunService = {
  list: async (agentId: string, page = 1, pageSize = 20): Promise<PagedResult<AgentRunSummary>> => {
    return api.get<PagedResult<AgentRunSummary>>(
      `/ai/agents/${agentId}/runs?page=${page}&pageSize=${pageSize}`,
    );
  },
};
