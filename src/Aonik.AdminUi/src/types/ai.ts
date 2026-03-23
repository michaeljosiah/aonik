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

export interface ListAiCatalogModelProvidersResponse {
  modelProviders: AiCatalogModelProviderResponse[];
}

export interface ListAiCatalogModelsResponse {
  models: AiCatalogModelResponse[];
}

export interface ListAgentsResponse {
  agents: AgentInfo[];
}
