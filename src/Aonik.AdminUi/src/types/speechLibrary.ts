/**
 * Wire types mirroring the backend speech library contract
 * (`Aonik.SharedKernel.Abstractions.Ai.Speech`). See
 * `docs/specifications/024.unified-speech-config-and-composer.md` §"Domain model".
 *
 * Polymorphic `SpeechProviderConfig` round-trips with a `kind` discriminator
 * (`openai-whisper` / `azure-stt` / `openai-tts` / `azure-tts` /
 * `elevenlabs-tts` / `mistral-tts` / `openai-realtime` / `azure-voice-live`).
 */

// ── Enums (string-serialised on the wire) ─────────────────────────────────

export type SpeechProviderType = 'Stt' | 'Tts' | 'Composite';
export type SpeechProviderStatus = 'Active' | 'Disabled' | 'SoftDeleted';
export type SpeechProviderHistoryAction = 'Created' | 'Updated' | 'StatusChanged' | 'SoftDeleted';

// ── Polymorphic config payloads ───────────────────────────────────────────

export type SpeechProviderConfig =
  | OpenAIWhisperConfig
  | AzureSttConfig
  | OpenAITtsConfig
  | AzureTtsConfig
  | ElevenLabsTtsConfig
  | MistralTtsConfig
  | OpenAIRealtimeCompositeConfig
  | AzureVoiceLiveCompositeConfig;

// Phase D: configs are vendor-level only. Voice + model selection moved to the
// consumer (recipe / chat-speech) so different recipes can use different voices on
// the same vendor. Defaults declared here are inheritance starting points.

export interface OpenAIWhisperConfig {
  kind: 'openai-whisper';
  defaultModel: string | null;
  defaultLanguage: string | null;
}

export interface AzureSttConfig {
  kind: 'azure-stt';
  region: string;
  defaultLanguage: string | null;
}

export interface OpenAITtsConfig {
  kind: 'openai-tts';
  defaultModelId: string | null;
}

export interface AzureTtsConfig {
  kind: 'azure-tts';
  region: string;
}

export interface ElevenLabsTtsConfig {
  kind: 'elevenlabs-tts';
  defaultModelId: string | null;
  defaultStability: number | null;
  defaultSimilarityBoost: number | null;
  defaultOptimizeStreamingLatency: number | null;
}

export interface MistralTtsConfig {
  kind: 'mistral-tts';
  defaultModelId: string | null;
}

export interface OpenAIRealtimeCompositeConfig {
  kind: 'openai-realtime';
  defaultModel: string | null;
  defaultInstructionsAddendum: string | null;
}

export interface AzureVoiceLiveCompositeConfig {
  kind: 'azure-voice-live';
  region: string;
  endpoint: string;
  defaultModel: string | null;
  defaultInstructionsAddendum: string | null;
}

// ── Library entries ───────────────────────────────────────────────────────

export interface SpeechProvider {
  /** Built-in archetypes use `built-in:{name}`; tenant rows use Guid in N format. */
  id: string;
  displayName: string;
  type: SpeechProviderType;
  vendor: string;
  config: SpeechProviderConfig;
  status: SpeechProviderStatus;
  /**
   * True iff a tenant API key is stored on this row (Phase D). Status-only readback —
   * the encrypted key itself is never returned. Falsey doesn't mean unauthenticated;
   * the resolver still falls back to host default + configuration.
   */
  hasApiKey: boolean;
  /** True for shipped archetypes; false for tenant-owned. */
  isBuiltIn: boolean;
  /** Increments on every update; built-ins are always 1. */
  version: number;
  createdAt: string;
  updatedAt: string;
  createdByUserId: string | null;
  lastUpdatedByUserId: string | null;
}

export interface SpeechProviderHistoryEntry {
  version: number;
  action: SpeechProviderHistoryAction;
  snapshotDisplayName: string;
  snapshotStatus: SpeechProviderStatus;
  snapshotConfig: SpeechProviderConfig;
  at: string;
  byUserId: string | null;
}

export interface SpeechProviderUsage {
  recipesUsingThisProvider: SpeechProviderUsageRecipeRef[];
}

export interface SpeechProviderUsageRecipeRef {
  recipeId: string;
  displayName: string;
  isActiveVoiceRecipe: boolean;
}

// ── Request payloads ──────────────────────────────────────────────────────

export interface CreateSpeechProviderRequest {
  displayName: string;
  type: SpeechProviderType;
  vendor: string;
  config: SpeechProviderConfig;
  /**
   * Plaintext API key. When present it's encrypted at rest and stored on the provider
   * row, becoming the tenant override in the unified credential resolver. Pass null
   * to leave the row keyless (admin can fill it in later).
   */
  apiKey?: string | null;
}

export interface UpdateSpeechProviderRequest {
  displayName: string;
  config: SpeechProviderConfig;
  /**
   * Tri-state. `null` (or `undefined`) leaves the existing credential alone. Empty
   * string clears the stored credential. Non-empty encrypts + replaces.
   */
  apiKey?: string | null;
}

export interface CloneSpeechProviderRequest {
  newDisplayName?: string | null;
}

export interface SetSpeechProviderStatusRequest {
  status: SpeechProviderStatus;
}

// ── Vendor catalog (form schema) ──────────────────────────────────────────

export interface SpeechVendorsCatalogResponse {
  vendors: SpeechVendorDescriptor[];
}

export interface SpeechVendorDescriptor {
  vendor: string;
  displayName: string;
  supportedTypes: SpeechProviderType[];
  forms: SpeechVendorFormSchema[];
}

export interface SpeechVendorFormSchema {
  type: SpeechProviderType;
  /** Discriminator value to use when constructing `SpeechProviderConfig.kind`. */
  configKind: SpeechProviderConfig['kind'];
  fields: SpeechVendorFormField[];
}

export type SpeechVendorWidget =
  | 'text'
  | 'password'
  | 'select'
  | 'remote-select'
  | 'number'
  | 'textarea';

export interface SpeechVendorFormField {
  name: string;
  label: string;
  widget: SpeechVendorWidget;
  required: boolean;
  description?: string | null;
  placeholder?: string | null;
  default?: string | null;
  options?: SpeechVendorFormOption[] | null;
  min?: number | null;
  max?: number | null;
  /** For `remote-select`: identifier the front-end uses to pick the loader function. */
  remoteOptionsKey?: string | null;
}

export interface SpeechVendorFormOption {
  value: string;
  label: string;
  description?: string | null;
}

// ── STT test response ─────────────────────────────────────────────────────

export interface TestSpeechProviderSttResponse {
  text: string;
  language: string | null;
}
