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

export interface AiTraceListItemResponse {
  runId: string;
  startedAt: string;
  useCase: string;
  outcome: string;
  requestedModel: string | null;
  actualModel: string | null;
  latencyMs: number | null;
  ttftMs: number | null;
  inputTokens: number | null;
  outputTokens: number | null;
  totalTokens: number | null;
  estimatedCostUsd: number | null;
  traceStatus: string;
}

export interface ListAiTracesResponse {
  items: AiTraceListItemResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AiTraceRunRecordResponse {
  runId: string;
  startedAt: string;
  useCase: string;
  aiModelId: string;
  aiModelName: string | null;
  promptSpecId: string | null;
  aiPolicyId: string | null;
  inputRefsJson: string;
  outputRef: string | null;
  tokensUsed: number;
  costEstimate: number;
  latencyMs: number;
  outcome: string;
}

export interface AiTraceMetricsResponse {
  requestedModel: string | null;
  actualModel: string | null;
  latencyMs: number | null;
  ttftMs: number | null;
  inputTokens: number | null;
  outputTokens: number | null;
  totalTokens: number | null;
  estimatedCostUsd: number | null;
  completedAt: string | null;
}

export interface AiTraceTimelineEventResponse {
  timestamp: string;
  eventType: string;
  title: string;
  description: string | null;
  status: string | null;
}

export interface AiTraceRawTelemetryEventResponse {
  timestamp: string;
  message: string;
  dimensions: Record<string, string | null>;
}

export interface AiTraceRunDetailResponse {
  run: AiTraceRunRecordResponse;
  metrics: AiTraceMetricsResponse | null;
  timeline: AiTraceTimelineEventResponse[];
  rawTelemetry: AiTraceRawTelemetryEventResponse[];
  traceStatus: string;
}

export interface AiTraceObservationResponse {
  observationId: string;
  traceId: string;
  parentObservationId: string | null;
  spanId: string | null;
  parentSpanId: string | null;
  operationId: string | null;
  aiRunId: string | null;
  startTime: string;
  endTime: string | null;
  type: string;
  name: string;
  traceName: string | null;
  input: string | null;
  output: string | null;
  metadata: string | null;
  agentId: string | null;
  agentName: string | null;
  serviceName: string | null;
  level: string;
  durationMs: number | null;
  latencySeconds: number | null;
  costUsd: number | null;
  timeToFirstTokenSeconds: number | null;
  providedModel: string | null;
  inputTokens: number | null;
  outputTokens: number | null;
  totalTokens: number | null;
  isRootObservation: boolean;
  source: string;
}

export interface ListAiTraceObservationsResponse {
  items: AiTraceObservationResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
  provider: string;
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

// ── Playground service ──────────────────────────────────────────────

export interface ProjectUserBriefResponse {
  /** Internal user id the brief was projected for; pass as impersonateUserId on /ai/playground/run so personal-finance sub-agents target the briefed user's data. */
  userId: string;
  /** The actual brief payload that gets JSON.stringified into the system prompt. */
  brief: unknown;
}

export const playgroundService = {
  /** Project a real user brief by user ID or party ID (admin only). */
  projectUserBrief: async (options: { userId?: string; partyId?: string }): Promise<ProjectUserBriefResponse> => {
    return api.post<ProjectUserBriefResponse>('/ai/playground/user-brief', options);
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

export interface ListAiRunsParams {
  /** Optional — filters by AiRun.UseCase. Omit to list every run in the tenant. */
  useCase?: string;
  /** Optional — filters by AiRun.Outcome (e.g. "Success", "Failed"). */
  outcome?: string;
  page?: number;
  pageSize?: number;
}

export const aiRunService = {
  list: async (params: ListAiRunsParams = {}): Promise<ListAiRunsResponse> => {
    const search = new URLSearchParams();
    if (params.useCase) search.set('useCase', params.useCase);
    if (params.outcome) search.set('outcome', params.outcome);
    if (params.page) search.set('page', String(params.page));
    if (params.pageSize) search.set('pageSize', String(params.pageSize));
    const qs = search.toString();
    return api.get<ListAiRunsResponse>(`/ai/runs${qs ? `?${qs}` : ''}`);
  },
};

// ── AI Policy service ──────────────────────────────────────────────

export interface AiPolicySummary {
  id: string;
  name: string;
  isActive: boolean;
  allowedDataFieldsJson: string;
  redactionRulesJson: string;
  bannedActionsJson: string;
  escalationRulesJson: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ListAiPoliciesResponse {
  items: AiPolicySummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const aiPolicyService = {
  list: async (page: number = 1, pageSize: number = 50): Promise<ListAiPoliciesResponse> => {
    return api.get<ListAiPoliciesResponse>(
      `/admin/ai/policies?page=${page}&pageSize=${pageSize}`,
    );
  },
  /** Toggles IsActive on a single policy and returns the updated row. */
  setActive: async (id: string, isActive: boolean): Promise<AiPolicySummary> => {
    return api.patch<AiPolicySummary>(`/admin/ai/policies/${encodeURIComponent(id)}`, {
      isActive,
    });
  },
};

// ── Tenant agent settings (global kill switch) ─────────────────────

export interface TenantAgentSettingsResponse {
  killSwitchEngaged: boolean;
  killSwitchEngagedAt: string | null;
  killSwitchEngagedByUserId: string | null;
  updatedAt: string | null;
}

export const tenantAgentSettingsService = {
  get: async (): Promise<TenantAgentSettingsResponse> => {
    return api.get<TenantAgentSettingsResponse>('/admin/ai/agent-settings');
  },
  setKillSwitch: async (engaged: boolean): Promise<TenantAgentSettingsResponse> => {
    return api.patch<TenantAgentSettingsResponse>('/admin/ai/agent-settings', {
      killSwitchEngaged: engaged,
    });
  },
};

export const aiTraceService = {
  list: async (options?: {
    page?: number;
    pageSize?: number;
    useCase?: string;
    outcome?: string;
    timeRange?: string;
    runId?: string;
  }): Promise<ListAiTracesResponse> => {
    const params = new URLSearchParams();
    if (options?.page) params.set('page', String(options.page));
    if (options?.pageSize) params.set('pageSize', String(options.pageSize));
    if (options?.useCase) params.set('useCase', options.useCase);
    if (options?.outcome) params.set('outcome', options.outcome);
    if (options?.timeRange) params.set('timeRange', options.timeRange);
    if (options?.runId) params.set('runId', options.runId);
    const query = params.toString();

    return api.get<ListAiTracesResponse>(`/ai/traces${query ? `?${query}` : ''}`);
  },

  get: async (runId: string): Promise<AiTraceRunDetailResponse> => {
    return api.get<AiTraceRunDetailResponse>(`/ai/traces/${runId}`);
  },

  listObservations: async (options?: {
    page?: number;
    pageSize?: number;
    type?: string;
    name?: string;
    traceName?: string;
    traceId?: string;
    agentName?: string;
    environment?: string;
    level?: string;
    isRootObservation?: boolean;
    timeRange?: string;
  }): Promise<ListAiTraceObservationsResponse> => {
    const params = new URLSearchParams();
    if (options?.page) params.set('page', String(options.page));
    if (options?.pageSize) params.set('pageSize', String(options.pageSize));
    if (options?.type) params.set('type', options.type);
    if (options?.name) params.set('name', options.name);
    if (options?.traceName) params.set('traceName', options.traceName);
    if (options?.traceId) params.set('traceId', options.traceId);
    if (options?.agentName) params.set('agentName', options.agentName);
    if (options?.environment) params.set('environment', options.environment);
    if (options?.level) params.set('level', options.level);
    if (options?.isRootObservation !== undefined) params.set('isRootObservation', String(options.isRootObservation));
    if (options?.timeRange) params.set('timeRange', options.timeRange);
    const query = params.toString();

    return api.get<ListAiTraceObservationsResponse>(`/ai/trace-observations${query ? `?${query}` : ''}`);
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
