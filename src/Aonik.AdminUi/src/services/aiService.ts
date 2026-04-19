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
  PromptSpecResponse,
  CreatePromptSpecRequest,
  UpdatePromptSpecRequest,
  ListPromptSpecsResponse,
  RoutePolicyResponse,
  CreateRoutePolicyRequest,
  UpdateRoutePolicyRequest,
  ListRoutePoliciesResponse,
} from '@/types/ai';
import type { PagedResult } from '@/types';

// ── AI Task Types ──────────────────────────────────────────────────

export interface AiTaskResponse {
  id: string;
  tenantId: string | null;
  useCase: string;
  displayName: string;
  description: string;
  category: string;
  executionMode: string;
  promptName: string;
  promptVersion: string;
  systemTemplate: string;
  userTemplate: string;
  developerTemplate: string;
  variablesSchemaJson: string;
  outputSchemaJson: string;
  isPublished: boolean;
  isActive: boolean;
  primaryModelId: string | null;
  primaryModelName: string | null;
  isOverride: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface AiTaskStatsResponse {
  totalRuns: number;
  last24hRuns: number;
  avgLatencyMs: number;
  avgCost: number;
  successRate: number;
  lastRunAt: string | null;
}

export interface AiTaskDetailResponse extends AiTaskResponse {
  stats: AiTaskStatsResponse;
  routePolicyId: string | null;
  routePolicyRiskTier: string | null;
  routePolicyDataSensitivity: string | null;
}

export interface CreateAiTaskRequest {
  useCase: string;
  displayName: string;
  description: string;
  category: string;
  executionMode: string;
  promptName: string;
  promptVersion: string;
  systemTemplate: string;
  userTemplate: string;
  developerTemplate?: string;
  variablesSchemaJson?: string;
  outputSchemaJson?: string;
  isPublished?: boolean;
  isActive?: boolean;
  primaryModelId?: string;
}

export interface UpdateAiTaskRequest {
  displayName?: string;
  description?: string;
  category?: string;
  executionMode?: string;
  promptName?: string;
  promptVersion?: string;
  systemTemplate?: string;
  userTemplate?: string;
  developerTemplate?: string;
  variablesSchemaJson?: string;
  outputSchemaJson?: string;
  isPublished?: boolean;
  isActive?: boolean;
  primaryModelId?: string;
}

export interface AiRunSummaryResponse {
  id: string;
  useCase: string;
  modelName: string | null;
  tokensUsed: number;
  costEstimate: number;
  latencyMs: number;
  outcome: string;
  createdAt: string;
}

export interface ListAiRunsResponse {
  items: AiRunSummaryResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

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

  improvePrompt: async (currentPrompt: string | null, userIntent: string): Promise<string> => {
    const result = await api.post<{ improvedPrompt: string }>('/ai/agents/improve-prompt', {
      currentPrompt,
      userIntent,
    });
    return result.improvedPrompt;
  },

  resetPrompt: async (agentName: string): Promise<AgentConfigurationResponse> => {
    return api.post<AgentConfigurationResponse>(
      `/ai/agents/configurations/${agentName}/reset-prompt`,
      {},
    );
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

// ── Prompt spec service ─────────────────────────────────────────────

export const promptSpecService = {
  list: async (name?: string): Promise<PromptSpecResponse[]> => {
    const query = name ? `?name=${encodeURIComponent(name)}` : '';
    const res = await api.get<ListPromptSpecsResponse>(`/ai/prompts${query}`);
    return res.prompts;
  },

  get: async (id: string): Promise<PromptSpecResponse> => {
    return api.get<PromptSpecResponse>(`/ai/prompts/${id}`);
  },

  create: async (request: CreatePromptSpecRequest): Promise<PromptSpecResponse> => {
    return api.post<PromptSpecResponse>('/ai/prompts', request);
  },

  update: async (id: string, request: UpdatePromptSpecRequest): Promise<PromptSpecResponse> => {
    return api.put<PromptSpecResponse>(`/ai/prompts/${id}`, request);
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/ai/prompts/${id}`);
  },
};

// ── Playground service ──────────────────────────────────────────────

export const playgroundService = {
  /** Project a real user brief by user ID or party ID (admin only). */
  projectUserBrief: async (options: { userId?: string; partyId?: string }): Promise<unknown> => {
    return api.post('/ai/playground/user-brief', options);
  },
};

// ── Route policy service ────────────────────────────────────────────

export const routePolicyService = {
  list: async (useCase?: string): Promise<RoutePolicyResponse[]> => {
    const query = useCase ? `?useCase=${encodeURIComponent(useCase)}` : '';
    const res = await api.get<ListRoutePoliciesResponse>(`/ai/route-policies${query}`);
    return res.policies;
  },

  get: async (id: string): Promise<RoutePolicyResponse> => {
    return api.get<RoutePolicyResponse>(`/ai/route-policies/${id}`);
  },

  create: async (request: CreateRoutePolicyRequest): Promise<RoutePolicyResponse> => {
    return api.post<RoutePolicyResponse>('/ai/route-policies', request);
  },

  update: async (id: string, request: UpdateRoutePolicyRequest): Promise<RoutePolicyResponse> => {
    return api.put<RoutePolicyResponse>(`/ai/route-policies/${id}`, request);
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/ai/route-policies/${id}`);
  },
};

// ── AI Task service ────────────────────────────────────────────────

export const aiTaskService = {
  list: async (category?: string): Promise<AiTaskResponse[]> => {
    const params = category ? `?category=${encodeURIComponent(category)}` : '';
    const res = await api.get<{ tasks: AiTaskResponse[] }>(`/ai/tasks${params}`);
    return res.tasks;
  },

  getDetail: async (id: string): Promise<AiTaskDetailResponse> => {
    return api.get<AiTaskDetailResponse>(`/ai/tasks/${id}`);
  },

  create: async (request: CreateAiTaskRequest): Promise<AiTaskResponse> => {
    return api.post<AiTaskResponse>('/ai/tasks', request);
  },

  update: async (id: string, request: UpdateAiTaskRequest): Promise<AiTaskResponse> => {
    return api.put<AiTaskResponse>(`/ai/tasks/${id}`, request);
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/ai/tasks/${id}`);
  },

  resetPrompt: async (id: string): Promise<AiTaskResponse> => {
    return api.post<AiTaskResponse>(`/ai/tasks/${id}/reset-prompt`, {});
  },
};

// ── AI Run service ─────────────────────────────────────────────────

export const aiRunService = {
  list: async (useCase: string, page: number = 1, pageSize: number = 20): Promise<ListAiRunsResponse> => {
    return api.get<ListAiRunsResponse>(
      `/ai/runs?useCase=${encodeURIComponent(useCase)}&page=${page}&pageSize=${pageSize}`,
    );
  },
};

// ── Playground Scenario service ───────────────────────────────────

import type {
  PlaygroundScenarioResponse,
  PlaygroundScenarioSummaryResponse,
  CreatePlaygroundScenarioRequest,
  UpdatePlaygroundScenarioRequest,
  GeneratePlaygroundScenarioRequest,
} from '../types/ai';

export const playgroundScenarioService = {
  list: async (agentName?: string, tag?: string): Promise<PlaygroundScenarioSummaryResponse[]> => {
    const params = new URLSearchParams();
    if (agentName) params.set('agentName', agentName);
    if (tag) params.set('tag', tag);
    const query = params.toString();
    const res = await api.get<{ scenarios: PlaygroundScenarioSummaryResponse[] }>(
      `/ai/playground/scenarios${query ? `?${query}` : ''}`,
    );
    return res.scenarios;
  },

  get: async (id: string): Promise<PlaygroundScenarioResponse> => {
    return api.get<PlaygroundScenarioResponse>(`/ai/playground/scenarios/${id}`);
  },

  create: async (request: CreatePlaygroundScenarioRequest): Promise<PlaygroundScenarioResponse> => {
    return api.post<PlaygroundScenarioResponse>('/ai/playground/scenarios', request);
  },

  update: async (id: string, request: UpdatePlaygroundScenarioRequest): Promise<PlaygroundScenarioResponse> => {
    return api.put<PlaygroundScenarioResponse>(`/ai/playground/scenarios/${id}`, request);
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/ai/playground/scenarios/${id}`);
  },

  generate: async (request: GeneratePlaygroundScenarioRequest): Promise<PlaygroundScenarioResponse> => {
    return api.post<PlaygroundScenarioResponse>('/ai/playground/scenarios/generate', request, {
      timeout: 120000, // 2 minutes — LLM generation can take 15-60s
    });
  },
};
