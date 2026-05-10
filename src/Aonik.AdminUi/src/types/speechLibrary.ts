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

export interface OpenAIWhisperConfig {
  kind: 'openai-whisper';
  model: string | null;
  language: string | null;
}

export interface AzureSttConfig {
  kind: 'azure-stt';
  region: string;
  language: string | null;
}

export interface OpenAITtsConfig {
  kind: 'openai-tts';
  voiceId: string;
  modelId: string | null;
}

export interface AzureTtsConfig {
  kind: 'azure-tts';
  region: string;
  voiceId: string;
}

export interface ElevenLabsTtsConfig {
  kind: 'elevenlabs-tts';
  voiceId: string;
  modelId: string | null;
  stability: number | null;
  similarityBoost: number | null;
  optimizeStreamingLatency: number | null;
}

export interface MistralTtsConfig {
  kind: 'mistral-tts';
  voiceId: string;
  modelId: string | null;
}

export interface OpenAIRealtimeCompositeConfig {
  kind: 'openai-realtime';
  voice: string;
  model: string | null;
  instructionsAddendum: string | null;
}

export interface AzureVoiceLiveCompositeConfig {
  kind: 'azure-voice-live';
  region: string;
  endpoint: string;
  voice: string;
  model: string | null;
  instructionsAddendum: string | null;
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
}

export interface UpdateSpeechProviderRequest {
  displayName: string;
  config: SpeechProviderConfig;
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

export type SpeechVendorWidget = 'text' | 'password' | 'select' | 'number' | 'textarea';

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
