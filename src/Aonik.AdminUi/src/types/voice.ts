/**
 * Wire types for the Voice & Speech admin settings page. These mirror the
 * record shapes returned by the backend endpoints in Aonik.Voice/Endpoints/Admin
 * (see docs/specifications/022.aonik-voice-realtime.md Phase 5).
 */

export interface VoiceProviderSettingsResponse {
  enabled: boolean;
  kind: string;
  recipeId: string | null;
  chained: ChainedVoiceSettingsResponse | null;
}

export interface ChainedVoiceSettingsResponse {
  stt: SttSettingsResponse;
  tts: TtsSettingsResponse;
  vad: VadSettingsResponse;
  transcriptionFilter: boolean;
  sentenceAggregator: boolean;
}

export interface SttSettingsResponse {
  vendor: string;
  model: string | null;
}

export interface TtsSettingsResponse {
  vendor: string;
  voiceId: string | null;
  modelId: string | null;
}

export interface VadSettingsResponse {
  kind: string;
  stopMs: number | null;
}

export type VoiceProviderSettingsUpdateRequest = VoiceProviderSettingsResponse;

export interface VoiceRecipeResponse {
  id: string;
  name: string;
  description: string;
  costRanking: string;
  latencyTarget: string;
  implemented: boolean;
  settings: VoiceProviderSettingsResponse;
}

export interface VoiceOptionResponse {
  id: string;
  name: string;
  description: string | null;
}

export interface VoicePreviewRequest {
  text: string;
  provider: string;
  voiceId: string;
  modelId?: string | null;
  /** Required for Azure (e.g. 'eastus'); ignored by other providers. */
  region?: string | null;
}

export interface VoicePreviewAudioResponse {
  audioBlob: Blob;
  contentType: string;
  provider: string | null;
  voiceId: string | null;
  /** Sample rate of the underlying PCM (set by the server in `X-Voice-Sample-Rate`). */
  sampleRate: number | null;
}

export interface SttPreviewRequest {
  /** 16-bit PCM (or WAV) bytes — captured from the mic or uploaded by the admin. */
  audio: Blob;
  provider: string;
  model?: string | null;
  language?: string | null;
  /** Required for Azure; ignored otherwise. */
  region?: string | null;
  /** PCM sample rate. Ignored when `audio` is a WAV (read from header). */
  sampleRate?: number;
}

export interface SttPreviewResponse {
  text: string;
  language: string | null;
}

export interface VoiceProviderCredentialResponse {
  provider: string;
  hasHostCredential: boolean;
  hasTenantOverride: boolean;
  effectiveSource: string;
}

export interface VoiceProviderCredentialUpdateRequest {
  apiKey?: string | null;
  clearStoredValue: boolean;
}
