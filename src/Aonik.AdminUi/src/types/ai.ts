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
  isOverride: boolean;
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
}

export interface AgentInfo {
  name: string;
  description: string;
}

// ── API response wrappers ───────────────────────────────────────────

export interface ListAiProvidersResponse {
  providers: AiProviderResponse[];
}

export interface ListAiModelsResponse {
  models: AiModelResponse[];
}

export interface ListAgentsResponse {
  agents: AgentInfo[];
}
