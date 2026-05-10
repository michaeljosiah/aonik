/**
 * Wire types for the voice recipe library (spec 024 Phase B). Mirrors the C# domain in
 * Aonik.SharedKernel.Abstractions.Ai.Speech.VoiceRecipe.
 */

export type VoiceRecipeKind = 'Chained' | 'Composite';
export type VoiceRecipeStatus = 'Active' | 'Disabled' | 'SoftDeleted';
export type VoiceRecipeHistoryAction = 'Created' | 'Updated' | 'StatusChanged' | 'SoftDeleted';

export interface ChainedRecipeBody {
  /** Stable provider id from the speech provider library (built-in id or tenant Guid). */
  sttProviderId: string;
  ttsProviderId: string;
  /**
   * Required voice id (Phase D) — voice selection moved off the provider config so
   * different recipes can use different voices on the same vendor.
   */
  ttsVoiceId: string;
  /** Optional model override; falls back to the provider's defaultModelId. */
  ttsModelId: string | null;
  /** Optional STT model override; falls back to the provider's defaultModel. */
  sttModel: string | null;
  /** Optional STT language hint (BCP-47); falls back to the provider's defaultLanguage. */
  sttLanguage: string | null;
  /** Null = use the client's hello.agentId; non-null overrides for every connection. */
  pinnedAgentId: string | null;
  vad: 'energy' | 'silero' | 'none' | string;
  vadStopMs: number | null;
  transcriptionFilter: boolean;
  sentenceAggregator: boolean;
}

export interface CompositeRecipeBody {
  compositeProviderId: string;
  /** Required voice (Phase D). */
  voice: string;
  /** Optional model override; falls back to the provider's defaultModel. */
  model: string | null;
  /** Optional per-recipe instruction addendum; appended to the resolved agent's instructions. */
  instructionsAddendum: string | null;
  pinnedAgentId: string | null;
}

export interface VoiceRecipe {
  id: string;
  displayName: string;
  description: string | null;
  kind: VoiceRecipeKind;
  chained: ChainedRecipeBody | null;
  composite: CompositeRecipeBody | null;
  isBuiltIn: boolean;
  status: VoiceRecipeStatus;
  version: number;
  createdAt: string;
  updatedAt: string;
  createdByUserId: string | null;
  lastUpdatedByUserId: string | null;
}

export interface VoiceRecipeHistoryEntry {
  version: number;
  action: VoiceRecipeHistoryAction;
  snapshotDisplayName: string;
  snapshotDescription: string | null;
  snapshotStatus: VoiceRecipeStatus;
  snapshotChained: ChainedRecipeBody | null;
  snapshotComposite: CompositeRecipeBody | null;
  at: string;
  byUserId: string | null;
}

export interface CreateVoiceRecipeRequest {
  displayName: string;
  description: string | null;
  kind: VoiceRecipeKind;
  chained: ChainedRecipeBody | null;
  composite: CompositeRecipeBody | null;
}

export interface UpdateVoiceRecipeRequest {
  displayName: string;
  description: string | null;
  chained: ChainedRecipeBody | null;
  composite: CompositeRecipeBody | null;
}
