/**
 * Wire types for the singleton-per-tenant active settings (spec 024 Phase C). Mirrors
 * `Aonik.SharedKernel.Abstractions.Ai.Speech.{VoiceModeSettings,ChatSpeechSettings}`.
 */

export interface VoiceModeSettings {
  /** Currently active recipe id (built-in id or tenant Guid). Null = no recipe selected. */
  activeRecipeId: string | null;
  enabled: boolean;
  updatedAt: string;
  lastUpdatedByUserId: string | null;
}

export interface UpdateVoiceModeSettingsRequest {
  activeRecipeId: string | null;
  enabled: boolean;
}

export interface ChatSpeechSettings {
  activeTtsProviderId: string | null;
  enabled: boolean;
  autoPlay: boolean;
  showSpeakButton: boolean;
  /** Playback rate as a percentage of natural pace. 100 = 1.0x; range 50–200. */
  ratePercent: number;
  updatedAt: string;
  lastUpdatedByUserId: string | null;
}

export interface UpdateChatSpeechSettingsRequest {
  activeTtsProviderId: string | null;
  enabled: boolean;
  autoPlay: boolean;
  showSpeakButton: boolean;
  ratePercent: number;
}
