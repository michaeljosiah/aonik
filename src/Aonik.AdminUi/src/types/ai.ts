// ── AI Provider types ────────────────────────────────────────────────

export interface AiProviderResponse {
  id: string;
  name: string;
  authConfigRef?: string | null;
  capabilitiesJson: string;
  isActive: boolean;
  models: AiModelResponse[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateAiProviderRequest {
  name: string;
  authConfigRef?: string | null;
  capabilitiesJson?: string;
  isActive?: boolean;
}

export interface UpdateAiProviderRequest {
  name?: string | null;
  authConfigRef?: string | null;
  capabilitiesJson?: string | null;
  isActive?: boolean | null;
}

// ── AI Model types ──────────────────────────────────────────────────

export interface AiModelResponse {
  id: string;
  aiProviderId: string;
  providerName?: string | null;
  modelName: string;
  contextWindow: number;
  costProfileJson: string;
  latencyProfileJson: string;
  policyTagsJson: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateAiModelRequest {
  aiProviderId: string;
  modelName: string;
  contextWindow?: number;
  costProfileJson?: string;
  latencyProfileJson?: string;
  policyTagsJson?: string;
  isActive?: boolean;
}

export interface UpdateAiModelRequest {
  modelName?: string | null;
  contextWindow?: number | null;
  costProfileJson?: string | null;
  latencyProfileJson?: string | null;
  policyTagsJson?: string | null;
  isActive?: boolean | null;
}

// ── External model catalog types ─────────────────────────────────────

export interface AiCatalogModelProviderResponse {
  modelProviderKey: string;
  name: string;
  documentationUrl?: string | null;
  sdkPackage?: string | null;
  apiBaseUrl?: string | null;
  environmentVariables: string[];
  modelCount: number;
}

export interface AiCatalogModelResponse {
  modelProviderKey: string;
  modelKey: string;
  name: string;
  family?: string | null;
  contextWindow: number;
  outputTokenLimit: number;
  costProfileJson: string;
  inputModalities: string[];
  outputModalities: string[];
  supportsReasoning: boolean;
  supportsToolCall: boolean;
  supportsStructuredOutput: boolean;
  supportsAttachments: boolean;
  isOpenWeights: boolean;
}

export interface ImportAiCatalogModelProviderRequest {
  importModelsAsInactive?: boolean;
}

export interface ImportAiCatalogModelProviderResponse {
  aiProviderId: string;
  modelProviderKey: string;
  providerName: string;
  providerCreated: boolean;
  modelsCreated: number;
  modelsLinked: number;
  modelsSkipped: number;
}

// ── Agent Configuration types ───────────────────────────────────────

export interface AgentConfigurationResponse {
  id: string;
  name: string;
  domain: string;
  description: string;
  instructionsText: string;
  toolsetIdsJson: string;
  permissionsProfileJson: string;
  riskTier: string;
  isActive: boolean;
  tenantId?: string | null;
  modelId?: string | null;
  modelName?: string | null;
  /** When true, the agent receives a projected User Brief as a system message. */
  requiresUserBrief: boolean;
  /** Optional URL for the agent's display icon/avatar image. */
  iconUrl?: string | null;
  isOverride: boolean;
  /** 0 = SubAgent, 1 = Orchestrator */
  agentType: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface UpsertAgentConfigurationRequest {
  description?: string | null;
  instructionsText?: string | null;
  toolsetIdsJson?: string | null;
  permissionsProfileJson?: string | null;
  riskTier?: string | null;
  isActive?: boolean | null;
  modelId?: string | null;
  /** Optional URL for the agent's display icon/avatar image. */
  iconUrl?: string | null;
}

export interface AgentInfo {
  name: string;
  description: string;
}

// ── Agent Run types ─────────────────────────────────────────────────

export interface AgentRunSummary {
  id: string;
  agentId: string;
  goal: string;
  status: string;
  stepCount: number;
  linkedAiRunCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

// ── Route Policy types ────────────────────────────────────────────

export interface RoutePolicyResponse {
  id: string;
  tenantId?: string | null;
  useCase: string;
  riskTier: string;
  dataSensitivity: string;
  primaryModelId: string;
  primaryModelName?: string | null;
  isActive: boolean;
  isOverride: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateRoutePolicyRequest {
  useCase: string;
  riskTier: string;
  dataSensitivity: string;
  primaryModelId: string;
  isActive: boolean;
}

export interface UpdateRoutePolicyRequest {
  riskTier?: string | null;
  dataSensitivity?: string | null;
  primaryModelId?: string | null;
  isActive?: boolean | null;
}

// ── Playground types ───────────────────────────────────────────────

export interface PlaygroundRunRecord {
  id: string;
  timestamp: Date;
  modelId?: string;
  modelName?: string;
  agentName?: string;
  systemPrompt: string;
  userMessage: string;
  assistantResponse: string;
  metrics: {
    inputTokens: number;
    outputTokens: number;
    totalTokens: number;
    latencyMs: number;
    estimatedCostUsd?: number;
  };
}

// ── Playground Review types ────────────────────────────────────────

export interface PlaygroundReviewMetric {
  name: string;
  score: number;
  explanation: string;
}

export interface PlaygroundReviewResult {
  overallScore: number;
  metrics: PlaygroundReviewMetric[];
  strengths: string[];
  suggestions: string[];
  promptImprovements: string[];
}

// ── Playground Scenario types ──────────────────────────────────────

export interface PlaygroundScenarioTurnResponse {
  id: string;
  role: string;
  content: string;
  sortOrder: number;
}

export interface PlaygroundScenarioResponse {
  id: string;
  name: string;
  description?: string | null;
  tags: string[];
  systemPrompt?: string | null;
  userBriefJson?: string | null;
  agentName?: string | null;
  aiTaskId?: string | null;
  modelId?: string | null;
  promptVariables?: Record<string, string> | null;
  turns: PlaygroundScenarioTurnResponse[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface PlaygroundScenarioSummaryResponse {
  id: string;
  name: string;
  description?: string | null;
  tags: string[];
  agentName?: string | null;
  aiTaskId?: string | null;
  turnCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreatePlaygroundScenarioRequest {
  name: string;
  description?: string | null;
  tags?: string[];
  systemPrompt?: string | null;
  userBriefJson?: string | null;
  agentName?: string | null;
  aiTaskId?: string | null;
  modelId?: string | null;
  promptVariables?: Record<string, string>;
  turns: { role: string; content: string }[];
}

export interface UpdatePlaygroundScenarioRequest {
  name?: string;
  description?: string;
  tags?: string[];
  systemPrompt?: string;
  turns?: { role: string; content: string }[];
}

export interface GeneratePlaygroundScenarioRequest {
  instructions: string;
  agentName?: string | null;
  aiTaskId?: string | null;
  modelId?: string | null;
}

// ── API response wrappers ───────────────────────────────────────────

export interface ListAiProvidersResponse {
  providers: AiProviderResponse[];
}

export interface ListAiModelsResponse {
  models: AiModelResponse[];
}

export interface ListAiCatalogModelProvidersResponse {
  modelProviders: AiCatalogModelProviderResponse[];
}

export interface ListAiCatalogModelsResponse {
  models: AiCatalogModelResponse[];
}

export interface ListAgentsResponse {
  agents: AgentInfo[];
}

export interface ListRoutePoliciesResponse {
  policies: RoutePolicyResponse[];
}
