import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, AudioLines, CircleHelp, Plus, RefreshCw, Save, Trash2, Upload, Volume2, Waves } from 'lucide-react';
import { toast } from 'sonner';

import { useAuth } from '@/auth';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { isPortalAdmin } from '@/lib/roleUtils';
import { getSelectedTenant } from '@/lib/tenantContext';
import { identityService } from '@/services/identityService';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';
import type { CreateVoiceRequest } from '@/services/textToSpeechSettingsService';
import type {
  TextToSpeechCredentialResponse,
  TextToSpeechSettingsResponse,
  TextToSpeechSettingsUpdateRequest,
  TextToSpeechVoiceOptionResponse,
} from '@/types';

interface TextToSpeechFormState {
  enabled: boolean;
  fallbackToNativeOnFailure: boolean;
  provider: string;
  voiceId: string;
  modelId: string;
  locale: string;
  outputFormat: string;
  optimizeStreamingLatency: string;
  stability: string;
  similarityBoost: string;
  maxCharactersPerUtterance: string;
  maxRequestsPerMinutePerUser: string;
  monthlyCharacterBudget: string;
  previewText: string;
  hostApiKey: string;
  tenantApiKey: string;
}

const DEFAULT_SELECT_VALUE = '__default__';
const UNAVAILABLE_VOICE_PREFIX = '__unavailable_voice__:';

const PROVIDER_OPTIONS = [
  { value: 'ElevenLabs', label: 'ElevenLabs' },
  { value: 'Mistral', label: 'Mistral (Voxtral)' },
] as const;

// ── Provider-specific option maps ────────────────────────────────────

const ELEVENLABS_MODEL_OPTIONS = [
  { value: 'eleven_multilingual_v2', label: 'Multilingual v2' },
  { value: 'eleven_turbo_v2_5', label: 'Turbo v2.5' },
  { value: 'eleven_flash_v2_5', label: 'Flash v2.5' },
] as const;

const MISTRAL_MODEL_OPTIONS = [
  { value: 'voxtral-mini-tts-2603', label: 'Voxtral Mini TTS' },
] as const;

const ELEVENLABS_OUTPUT_FORMAT_OPTIONS = [
  { value: 'mp3_22050_32', label: 'MP3 22.05 kHz · 32 kbps' },
  { value: 'mp3_24000_48', label: 'MP3 24 kHz · 48 kbps' },
  { value: 'mp3_44100_32', label: 'MP3 44.1 kHz · 32 kbps' },
  { value: 'mp3_44100_64', label: 'MP3 44.1 kHz · 64 kbps' },
  { value: 'mp3_44100_96', label: 'MP3 44.1 kHz · 96 kbps' },
  { value: 'mp3_44100_128', label: 'MP3 44.1 kHz · 128 kbps' },
  { value: 'mp3_44100_192', label: 'MP3 44.1 kHz · 192 kbps' },
  { value: 'pcm_8000', label: 'PCM 8 kHz' },
  { value: 'pcm_16000', label: 'PCM 16 kHz' },
  { value: 'pcm_22050', label: 'PCM 22.05 kHz' },
  { value: 'pcm_24000', label: 'PCM 24 kHz' },
  { value: 'pcm_32000', label: 'PCM 32 kHz' },
  { value: 'pcm_44100', label: 'PCM 44.1 kHz' },
  { value: 'pcm_48000', label: 'PCM 48 kHz' },
  { value: 'alaw_8000', label: 'A-law 8 kHz' },
  { value: 'ulaw_8000', label: 'u-law 8 kHz' },
  { value: 'opus_48000_32', label: 'Opus 48 kHz · 32 kbps' },
  { value: 'opus_48000_64', label: 'Opus 48 kHz · 64 kbps' },
  { value: 'opus_48000_96', label: 'Opus 48 kHz · 96 kbps' },
  { value: 'opus_48000_128', label: 'Opus 48 kHz · 128 kbps' },
  { value: 'opus_48000_192', label: 'Opus 48 kHz · 192 kbps' },
] as const;

const MISTRAL_OUTPUT_FORMAT_OPTIONS = [
  { value: 'mp3', label: 'MP3' },
  { value: 'wav', label: 'WAV' },
  { value: 'pcm', label: 'PCM (lowest latency)' },
  { value: 'flac', label: 'FLAC (lossless)' },
  { value: 'opus', label: 'Opus (low bitrate streaming)' },
] as const;

const LOCALE_OPTIONS = [
  { value: 'en-US', label: 'English (US)' },
  { value: 'en-GB', label: 'English (UK)' },
  { value: 'en-NG', label: 'English (Nigeria)' },
  { value: 'fr-FR', label: 'French' },
  { value: 'de-DE', label: 'German' },
  { value: 'es-ES', label: 'Spanish' },
  { value: 'it-IT', label: 'Italian' },
  { value: 'pt-BR', label: 'Portuguese (Brazil)' },
  { value: 'ar-SA', label: 'Arabic' },
  { value: 'hi-IN', label: 'Hindi' },
  { value: 'ja-JP', label: 'Japanese' },
] as const;

const OPTIMIZE_STREAMING_LATENCY_OPTIONS = [
  { value: '0', label: '0 · Best quality' },
  { value: '1', label: '1 · Balanced quality' },
  { value: '2', label: '2 · Balanced speed' },
  { value: '3', label: '3 · Faster response' },
  { value: '4', label: '4 · Lowest latency' },
] as const;

const UNIT_INTERVAL_OPTIONS = Array.from({ length: 21 }, (_, index) => {
  const value = (index * 0.05).toFixed(2);
  return { value, label: value };
});

function getModelOptions(provider: string) {
  return provider === 'Mistral' ? MISTRAL_MODEL_OPTIONS : ELEVENLABS_MODEL_OPTIONS;
}

function getOutputFormatOptions(provider: string) {
  return provider === 'Mistral' ? MISTRAL_OUTPUT_FORMAT_OPTIONS : ELEVENLABS_OUTPUT_FORMAT_OPTIONS;
}

function getDefaultModel(provider: string) {
  return provider === 'Mistral' ? 'voxtral-mini-tts-2603' : 'eleven_multilingual_v2';
}

function getDefaultOutputFormat(provider: string) {
  return provider === 'Mistral' ? 'mp3' : 'mp3_44100_128';
}

const PROVIDER_VALUES = PROVIDER_OPTIONS.map((option) => option.value);
const LOCALE_VALUES = LOCALE_OPTIONS.map((option) => option.value);
const OPTIMIZE_STREAMING_LATENCY_VALUES = OPTIMIZE_STREAMING_LATENCY_OPTIONS.map((option) => option.value);
const UNIT_INTERVAL_VALUES = UNIT_INTERVAL_OPTIONS.map((option) => option.value);

function normalizeAllowedValue(value: string | null | undefined, allowedValues: readonly string[], fallback: string) {
  const trimmed = toInputValue(value).trim();
  return allowedValues.includes(trimmed) ? trimmed : fallback;
}

function normalizeLatencyValue(value: string | null | undefined) {
  const parsed = Number.parseInt(toInputValue(value), 10);
  const normalized = Number.isNaN(parsed) ? '' : String(parsed);
  return OPTIMIZE_STREAMING_LATENCY_VALUES.some((allowedValue) => allowedValue === normalized) ? normalized : '3';
}

function normalizeUnitIntervalValue(value: string | null | undefined) {
  const trimmed = toInputValue(value).trim();
  if (!trimmed) {
    return '';
  }

  const parsed = Number.parseFloat(trimmed);
  if (Number.isNaN(parsed) || parsed < 0 || parsed > 1) {
    return '';
  }

  const normalized = parsed.toFixed(2);
  return UNIT_INTERVAL_VALUES.includes(normalized) ? normalized : '';
}

function FieldHelp({ title, description }: { title: string; description: string }) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          className="h-5 w-5 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
        >
          <CircleHelp className="h-3.5 w-3.5" />
          <span className="sr-only">More information about {title}</span>
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-72 space-y-1">
        <p className="text-sm font-medium text-[var(--color-text-primary)]">{title}</p>
        <p className="text-xs leading-5 text-[var(--color-text-secondary)]">{description}</p>
      </PopoverContent>
    </Popover>
  );
}

function FieldLabel({
  label,
  htmlFor,
  helpTitle,
  helpDescription,
}: {
  label: string;
  htmlFor?: string;
  helpTitle: string;
  helpDescription: string;
}) {
  return (
    <div className="flex items-center gap-1">
      <Label htmlFor={htmlFor}>{label}</Label>
      <FieldHelp title={helpTitle} description={helpDescription} />
    </div>
  );
}

function toInputValue(value?: string | null) {
  return value ?? '';
}

function toTrimmed(value: string) {
  return value.trim();
}

function resolveUserMessage(error: unknown, fallbackMessage: string) {
  const message = error && typeof error === 'object' && 'userMessage' in error
    ? String((error as { userMessage?: string }).userMessage ?? '')
    : '';

  return message || fallbackMessage;
}

function buildFormState(snapshot: TextToSpeechSettingsResponse): TextToSpeechFormState {
  const provider = normalizeAllowedValue(snapshot.defaultProfile.provider, PROVIDER_VALUES, 'ElevenLabs');
  const modelValues = getModelOptions(provider).map((o) => o.value) as readonly string[];
  const formatValues = getOutputFormatOptions(provider).map((o) => o.value) as readonly string[];

  return {
    enabled: snapshot.enabled,
    fallbackToNativeOnFailure: snapshot.fallbackToNativeOnFailure,
    provider,
    voiceId: toInputValue(snapshot.defaultProfile.voiceId),
    modelId: normalizeAllowedValue(snapshot.defaultProfile.modelId, modelValues, getDefaultModel(provider)),
    locale: normalizeAllowedValue(snapshot.defaultProfile.locale, LOCALE_VALUES, 'en-US'),
    outputFormat: normalizeAllowedValue(snapshot.defaultProfile.outputFormat, formatValues, getDefaultOutputFormat(provider)),
    optimizeStreamingLatency: normalizeLatencyValue(snapshot.defaultProfile.providerOptions.optimizeStreamingLatency),
    stability: normalizeUnitIntervalValue(snapshot.defaultProfile.providerOptions.stability),
    similarityBoost: normalizeUnitIntervalValue(snapshot.defaultProfile.providerOptions.similarityBoost),
    maxCharactersPerUtterance: String(snapshot.policy.maxCharactersPerUtterance),
    maxRequestsPerMinutePerUser: String(snapshot.policy.maxRequestsPerMinutePerUser),
    monthlyCharacterBudget: snapshot.policy.monthlyCharacterBudget != null
      ? String(snapshot.policy.monthlyCharacterBudget)
      : '',
    previewText: 'Your transport spending went up this week. Review the details in the conversation before approving any changes.',
    hostApiKey: '',
    tenantApiKey: '',
  };
}

function toCredentialBadgeVariant(value: boolean): 'success' | 'outline' {
  return value ? 'success' : 'outline';
}

function buildRequest(formState: TextToSpeechFormState): TextToSpeechSettingsUpdateRequest {
  const modelValues = getModelOptions(formState.provider).map((o) => o.value) as readonly string[];
  const formatValues = getOutputFormatOptions(formState.provider).map((o) => o.value) as readonly string[];

  return {
    enabled: formState.enabled,
    fallbackToNativeOnFailure: formState.fallbackToNativeOnFailure,
    defaultProfile: {
      provider: normalizeAllowedValue(formState.provider, PROVIDER_VALUES, 'ElevenLabs'),
      voiceId: toTrimmed(formState.voiceId),
      modelId: normalizeAllowedValue(formState.modelId, modelValues, getDefaultModel(formState.provider)),
      locale: normalizeAllowedValue(formState.locale, LOCALE_VALUES, 'en-US'),
      outputFormat: normalizeAllowedValue(formState.outputFormat, formatValues, getDefaultOutputFormat(formState.provider)),
      providerOptions: {
        optimizeStreamingLatency: normalizeLatencyValue(formState.optimizeStreamingLatency),
        stability: normalizeUnitIntervalValue(formState.stability) || null,
        similarityBoost: normalizeUnitIntervalValue(formState.similarityBoost) || null,
      },
    },
    policy: {
      maxCharactersPerUtterance: Number.parseInt(formState.maxCharactersPerUtterance, 10) || 280,
      maxRequestsPerMinutePerUser: Number.parseInt(formState.maxRequestsPerMinutePerUser, 10) || 20,
      monthlyCharacterBudget: toTrimmed(formState.monthlyCharacterBudget)
        ? Number.parseInt(formState.monthlyCharacterBudget, 10)
        : null,
    },
  };
}

export function SettingsTextToSpeechPage() {
  const { user } = useAuth();
  const selectedTenant = useMemo(() => getSelectedTenant(), []);
  const [formState, setFormState] = useState<TextToSpeechFormState | null>(null);
  const [voices, setVoices] = useState<TextToSpeechVoiceOptionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [savingHostCredential, setSavingHostCredential] = useState(false);
  const [savingTenantCredential, setSavingTenantCredential] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [previewAudio, setPreviewAudio] = useState<HTMLAudioElement | null>(null);
  const [hostCredential, setHostCredential] = useState<TextToSpeechCredentialResponse | null>(null);
  const [tenantCredential, setTenantCredential] = useState<TextToSpeechCredentialResponse | null>(null);
  const [voiceLoadError, setVoiceLoadError] = useState<string | null>(null);
  const [resolvedUserRoles, setResolvedUserRoles] = useState<string[]>(user?.roles ?? []);
  const [loadingUserRoles, setLoadingUserRoles] = useState(
    !!user && (user.roleSource === 'api' || !user.roles || user.roles.length === 0),
  );
  const [creatingVoice, setCreatingVoice] = useState(false);
  const [voiceCreateName, setVoiceCreateName] = useState('');
  const voiceFileRef = useRef<HTMLInputElement>(null);

  const isElevenLabs = formState?.provider === 'ElevenLabs';
  const isMistral = formState?.provider === 'Mistral';
  const modelOptions = formState ? getModelOptions(formState.provider) : ELEVENLABS_MODEL_OPTIONS;
  const outputFormatOptions = formState ? getOutputFormatOptions(formState.provider) : ELEVENLABS_OUTPUT_FORMAT_OPTIONS;

  const activeVoice = useMemo(
    () => voices.find((voice) => voice.voiceId === formState?.voiceId) ?? null,
    [voices, formState?.voiceId],
  );
  const savedVoiceUnavailable = useMemo(() => {
    const voiceId = toTrimmed(formState?.voiceId ?? '');
    return voiceId.length > 0 && !voices.some((voice) => voice.voiceId === voiceId);
  }, [voices, formState?.voiceId]);
  const voiceSelectValue = savedVoiceUnavailable
    ? `${UNAVAILABLE_VOICE_PREFIX}${formState?.voiceId ?? ''}`
    : formState?.voiceId ?? '';
  const previewCredentialConfigured = [tenantCredential?.effectiveSource, hostCredential?.effectiveSource]
    .some((source) => !!source && source !== 'Missing');
  const previewVoiceSelected = !!toTrimmed(formState?.voiceId ?? '');
  const previewTextProvided = !!toTrimmed(formState?.previewText ?? '');
  const previewBlockedMessage = !previewCredentialConfigured
    ? 'Add a host default, tenant override, or configuration fallback credential before previewing.'
    : !previewVoiceSelected
      ? 'Select a voice before previewing.'
        : !previewTextProvided
          ? 'Enter preview text before previewing.'
          : null;
  const isPlatformAdmin = isPortalAdmin(resolvedUserRoles);

  const updateField = <K extends keyof TextToSpeechFormState>(key: K, value: TextToSpeechFormState[K]) => {
    setFormState((prev) => (prev ? { ...prev, [key]: value } : prev));
  };

  const loadVoices = async (provider: string) => {
    try {
      const items = await textToSpeechSettingsService.listVoices(provider);
      setVoiceLoadError(null);
      setVoices(items);
    } catch (err: unknown) {
      setVoiceLoadError(resolveUserMessage(err, 'Failed to load voices.'));
      setVoices([]);
    }
  };

  const loadCredentials = async (provider: string) => {
    const [hostSnap, tenantSnap] = await Promise.all([
      isPlatformAdmin ? textToSpeechSettingsService.getHostCredential(provider) : Promise.resolve(null),
      textToSpeechSettingsService.getTenantCredential(provider),
    ]);
    setHostCredential(hostSnap);
    setTenantCredential(tenantSnap);
  };

  const loadSettings = async () => {
    setLoading(true);
    setError(null);

    try {
      const snapshot = await textToSpeechSettingsService.get();
      const nextState = buildFormState(snapshot);
      setFormState(nextState);
      await Promise.all([
        loadCredentials(nextState.provider),
        loadVoices(nextState.provider),
      ]);
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to load text-to-speech settings.'));
    } finally {
      setLoading(false);
    }
  };

  const handleProviderChange = useCallback(async (newProvider: string) => {
    updateField('provider', newProvider);
    updateField('modelId', getDefaultModel(newProvider));
    updateField('outputFormat', getDefaultOutputFormat(newProvider));
    updateField('voiceId', '');
    updateField('hostApiKey', '');
    updateField('tenantApiKey', '');

    await Promise.all([
      loadCredentials(newProvider),
      loadVoices(newProvider),
    ]);
  }, [isPlatformAdmin]);

  const handleSaveHostCredential = async (clearStoredValue = false) => {
    if (!formState) return;

    setSavingHostCredential(true);
    setError(null);

    try {
      const snapshot = await textToSpeechSettingsService.updateHostCredential({
        provider: formState.provider || 'ElevenLabs',
        apiKey: clearStoredValue ? null : toTrimmed(formState.hostApiKey) || null,
        clearStoredValue,
      });
      const tenantSnapshot = await textToSpeechSettingsService.getTenantCredential(formState.provider);

      setHostCredential(snapshot);
      setTenantCredential(tenantSnapshot);
      updateField('hostApiKey', '');
      await loadVoices(formState.provider || 'ElevenLabs');
      toast.success(clearStoredValue ? `Host ${formState.provider} key cleared.` : `Host ${formState.provider} key saved.`);
    } catch (err: unknown) {
      const message = resolveUserMessage(err, `Failed to update host ${formState.provider} key.`);
      setError(message);
      toast.error(message);
    } finally {
      setSavingHostCredential(false);
    }
  };

  const handleSaveTenantCredential = async (clearStoredValue = false) => {
    if (!formState) return;

    setSavingTenantCredential(true);
    setError(null);

    try {
      const snapshot = await textToSpeechSettingsService.updateTenantCredential({
        provider: formState.provider || 'ElevenLabs',
        apiKey: clearStoredValue ? null : toTrimmed(formState.tenantApiKey) || null,
        clearStoredValue,
      });

      setTenantCredential(snapshot);
      updateField('tenantApiKey', '');
      await loadVoices(formState.provider || 'ElevenLabs');
      toast.success(clearStoredValue ? `Tenant ${formState.provider} override cleared.` : `Tenant ${formState.provider} override saved.`);
    } catch (err: unknown) {
      const message = resolveUserMessage(err, `Failed to update tenant ${formState.provider} key.`);
      setError(message);
      toast.error(message);
    } finally {
      setSavingTenantCredential(false);
    }
  };

  const handleCreateVoice = useCallback(async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !formState) return;

    const name = toTrimmed(voiceCreateName);
    if (!name) {
      toast.error('Enter a voice name before uploading.');
      if (voiceFileRef.current) voiceFileRef.current.value = '';
      return;
    }

    setCreatingVoice(true);
    try {
      const arrayBuffer = await file.arrayBuffer();
      const base64 = btoa(
        new Uint8Array(arrayBuffer).reduce((data, byte) => data + String.fromCharCode(byte), ''),
      );

      const request: CreateVoiceRequest = {
        provider: formState.provider,
        name,
        sampleAudioBase64: base64,
        sampleFilename: file.name,
      };

      const result = await textToSpeechSettingsService.createVoice(request);
      toast.success(`Voice "${result.name}" created (${result.voiceId}).`);
      setVoiceCreateName('');
      updateField('voiceId', result.voiceId);
      await loadVoices(formState.provider);
    } catch (err: unknown) {
      toast.error(resolveUserMessage(err, 'Failed to create voice.'));
    } finally {
      setCreatingVoice(false);
      if (voiceFileRef.current) voiceFileRef.current.value = '';
    }
  }, [formState, voiceCreateName]);

  useEffect(() => {
    let active = true;

    const hydrateRoles = async () => {
      if (!user) {
        if (active) {
          setResolvedUserRoles([]);
          setLoadingUserRoles(false);
        }
        return;
      }

      if (user.roleSource === 'api' || !user.roles || user.roles.length === 0) {
        setLoadingUserRoles(true);
        try {
          const response = await identityService.getUserInfo();
          if (active) {
            setResolvedUserRoles(response.roles ?? []);
          }
        } catch {
          if (active) {
            setResolvedUserRoles(user.roles ?? []);
          }
        } finally {
          if (active) {
            setLoadingUserRoles(false);
          }
        }
        return;
      }

      if (active) {
        setResolvedUserRoles(user.roles);
        setLoadingUserRoles(false);
      }
    };

    void hydrateRoles();

    return () => {
      active = false;
    };
  }, [user]);

  useEffect(() => {
    if (loadingUserRoles) {
      return;
    }

    void loadSettings();
  }, [isPlatformAdmin, loadingUserRoles]);

  useEffect(() => {
    return () => {
      if (previewAudio) {
        previewAudio.pause();
        previewAudio.src = '';
      }
    };
  }, [previewAudio]);

  const handleSave = async () => {
    if (!formState) return;

    setSaving(true);
    setError(null);

    try {
      const updated = await textToSpeechSettingsService.update(buildRequest(formState));
      setFormState(buildFormState(updated));
      toast.success('Text-to-speech settings saved.');
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to save text-to-speech settings.'));
      toast.error(resolveUserMessage(err, 'Failed to save text-to-speech settings.'));
    } finally {
      setSaving(false);
    }
  };

  const handlePreview = async () => {
    if (!formState) return;

    if (!previewCredentialConfigured) {
      const message = 'Add a host default, tenant override, or configuration fallback credential before previewing.';
      setError(message);
      toast.error(message);
      return;
    }

    const voiceId = toTrimmed(formState.voiceId);
    if (!voiceId) {
      const message = 'Select a voice before previewing.';
      setError(message);
      toast.error(message);
      return;
    }

    const previewText = toTrimmed(formState.previewText);
    if (!previewText) {
      const message = 'Enter preview text before previewing.';
      setError(message);
      toast.error(message);
      return;
    }

    setPreviewing(true);
    setError(null);

    try {
      const preview = await textToSpeechSettingsService.preview({
        text: previewText,
        locale: formState.locale,
        provider: formState.provider,
        voiceId,
        modelId: formState.modelId,
        outputFormat: formState.outputFormat,
        providerOptions: {
          optimizeStreamingLatency: toTrimmed(formState.optimizeStreamingLatency) || null,
          stability: toTrimmed(formState.stability) || null,
          similarityBoost: toTrimmed(formState.similarityBoost) || null,
        },
      });

      previewAudio?.pause();
      previewAudio?.removeAttribute('src');

      const audioUrl = URL.createObjectURL(preview.audioBlob);
      const audio = new Audio(audioUrl);

      audio.onended = () => {
        URL.revokeObjectURL(audioUrl);
      };

      audio.onerror = () => {
        URL.revokeObjectURL(audioUrl);
      };

      setPreviewAudio(audio);
      await audio.play();

      toast.success(`Preview playing via ${preview.provider ?? formState.provider}. AiRunId: ${preview.aiRunId ?? 'n/a'}`);
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Preview failed.'));
      toast.error(resolveUserMessage(err, 'Preview failed.'));
    } finally {
      setPreviewing(false);
    }
  };

  const showHostCredentialCard = isPlatformAdmin;
  const providerLabel = formState?.provider ?? 'Provider';

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <AudioLines className="h-3.5 w-3.5" /> },
          { label: 'Text to Speech', icon: <Waves className="h-3.5 w-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Text to Speech</h1>
          <p className="text-[var(--color-text-secondary)]">
            Configure provider credentials, voice selection, playback behavior, and usage limits.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void loadSettings()} disabled={loading || saving || previewing}>
            <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button
            variant="outline"
            onClick={() => void handlePreview()}
            disabled={loading || saving || previewing || !formState || previewBlockedMessage != null}
          >
            <Volume2 className="mr-2 h-4 w-4" />
            {previewing ? 'Previewing...' : 'Preview'}
          </Button>
          <Button onClick={() => void handleSave()} disabled={loading || saving || previewing || !formState}>
            <Save className="mr-2 h-4 w-4" />
            Save settings
          </Button>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
            <AlertCircle className="h-5 w-5" />
            <span className="flex-1 text-sm">{error}</span>
          </CardContent>
        </Card>
      )}

      {loading || !formState ? (
        <Card>
          <CardContent className="flex items-center justify-center py-12 text-[var(--color-text-secondary)]">
            <RefreshCw className="mr-3 h-5 w-5 animate-spin" />
            Loading text-to-speech settings...
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-6">
          <div className={`grid gap-6 ${showHostCredentialCard ? 'xl:grid-cols-2' : ''}`}>
            {showHostCredentialCard && (
              <Card>
                <CardHeader>
                  <div className="flex items-center justify-between gap-2">
                    <div>
                      <CardTitle>Host Credential</CardTitle>
                      <CardDescription>Default {providerLabel} API key used when a tenant does not provide an override.</CardDescription>
                    </div>
                    <Badge variant={toCredentialBadgeVariant(hostCredential?.hasHostCredential ?? false)}>
                      {hostCredential?.hasHostCredential ? 'Configured' : 'Missing'}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="rounded-md border border-[var(--color-border-light)] px-4 py-3 text-sm text-[var(--color-text-secondary)]">
                    Effective source: <span className="font-medium text-[var(--color-text-primary)]">{hostCredential?.effectiveSource ?? 'Unknown'}</span>
                  </div>

                  <div className="space-y-2">
                    <div className="flex items-center justify-between gap-3">
                      <Label htmlFor="tts-host-api-key">{providerLabel} API Key (update only)</Label>
                    </div>
                    <Input
                      id="tts-host-api-key"
                      type="password"
                      value={formState.hostApiKey}
                      placeholder="Leave empty to keep existing host key"
                      onChange={(event) => updateField('hostApiKey', event.target.value)}
                    />
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <Button
                      onClick={() => void handleSaveHostCredential(false)}
                      disabled={savingHostCredential || saving || previewing}
                    >
                      <Save className="mr-2 h-4 w-4" />
                      Save host key
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => void handleSaveHostCredential(true)}
                      disabled={savingHostCredential || saving || previewing || !(hostCredential?.hasHostCredential ?? false)}
                    >
                      Clear host key
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}

            <Card>
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <div>
                    <CardTitle>Tenant Override</CardTitle>
                    <CardDescription>Optional {providerLabel} API key override for the currently selected tenant.</CardDescription>
                  </div>
                  <Badge variant={toCredentialBadgeVariant(tenantCredential?.hasTenantOverride ?? false)}>
                    {tenantCredential?.hasTenantOverride ? 'Override active' : 'Using host default'}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="rounded-md border border-[var(--color-border-light)] px-4 py-3 text-sm text-[var(--color-text-secondary)]">
                  Tenant: <span className="font-medium text-[var(--color-text-primary)]">{selectedTenant?.name ?? selectedTenant?.tenantId ?? 'No tenant selected'}</span>
                  <br />
                  Effective source: <span className="font-medium text-[var(--color-text-primary)]">{tenantCredential?.effectiveSource ?? 'Unknown'}</span>
                </div>

                <div className="space-y-2">
                  <div className="flex items-center justify-between gap-3">
                    <Label htmlFor="tts-tenant-api-key">Tenant {providerLabel} API Key (update only)</Label>
                  </div>
                  <Input
                    id="tts-tenant-api-key"
                    type="password"
                    value={formState.tenantApiKey}
                    placeholder="Leave empty to keep existing tenant override"
                    onChange={(event) => updateField('tenantApiKey', event.target.value)}
                  />
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button
                    onClick={() => void handleSaveTenantCredential(false)}
                    disabled={savingTenantCredential || saving || previewing || !selectedTenant?.tenantId}
                  >
                    <Save className="mr-2 h-4 w-4" />
                    Save tenant key
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => void handleSaveTenantCredential(true)}
                    disabled={savingTenantCredential || saving || previewing || !selectedTenant?.tenantId || !(tenantCredential?.hasTenantOverride ?? false)}
                  >
                    Clear tenant key
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between gap-2">
                <div>
                  <CardTitle>Playback Controls</CardTitle>
                  <CardDescription>Enable tenant TTS and decide whether device-native speech should take over on backend failure.</CardDescription>
                </div>
                <Badge variant={formState.enabled ? 'success' : 'outline'}>
                  {formState.enabled ? 'Enabled' : 'Disabled'}
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between rounded-md border border-[var(--color-border-light)] px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-[var(--color-text-primary)]">Tenant TTS enabled</p>
                  <p className="text-xs text-[var(--color-text-tertiary)]">Allow backend speech synthesis for this tenant.</p>
                </div>
                <Switch checked={formState.enabled} onCheckedChange={(checked) => updateField('enabled', checked)} />
              </div>

              <div className="flex items-center justify-between rounded-md border border-[var(--color-border-light)] px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-[var(--color-text-primary)]">Fallback to native on failure</p>
                  <p className="text-xs text-[var(--color-text-tertiary)]">Use device-native speech when backend synthesis or playback fails.</p>
                </div>
                <Switch
                  checked={formState.fallbackToNativeOnFailure}
                  onCheckedChange={(checked) => updateField('fallbackToNativeOnFailure', checked)}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Provider and Voice</CardTitle>
              <CardDescription>Configure the active provider, voice, model, and output profile used for speech synthesis.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <FieldLabel
                  label="Provider"
                  helpTitle="Provider"
                  helpDescription="The backend text-to-speech provider. ElevenLabs offers high-quality multilingual voices. Mistral (Voxtral) supports zero-shot voice cloning from short audio samples."
                />
                <Select value={formState.provider} onValueChange={(value) => void handleProviderChange(value)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select provider" />
                  </SelectTrigger>
                  <SelectContent>
                    {PROVIDER_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <FieldLabel
                  label="Voice"
                  helpTitle="Voice"
                  helpDescription="The voice used for playback. The list comes from the provider API for the currently effective credential."
                />
                <Select value={voiceSelectValue} onValueChange={(value) => updateField('voiceId', value)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select voice" />
                  </SelectTrigger>
                  <SelectContent>
                    {savedVoiceUnavailable && formState.voiceId && (
                      <SelectItem value={voiceSelectValue} disabled>
                        Current saved voice unavailable
                      </SelectItem>
                    )}
                    {voices.map((voice) => (
                      <SelectItem key={voice.voiceId} value={voice.voiceId}>
                        {voice.name}
                        {voice.labels?.gender ? ` (${voice.labels.gender})` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {activeVoice && (
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    {activeVoice.category ?? 'General'}
                    {activeVoice.labels?.gender ? ` · ${activeVoice.labels.gender}` : ''}
                    {activeVoice.labels?.accent ? ` · ${activeVoice.labels.accent}` : ''}
                    {activeVoice.labels?.languages ? ` · ${activeVoice.labels.languages}` : ''}
                  </p>
                )}
                {savedVoiceUnavailable && formState.voiceId && (
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    The saved voice is not available for the current effective credential.
                  </p>
                )}
                {voices.length === 0 && (
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    No voices loaded yet. Configure a valid credential, then refresh or reselect the provider.
                  </p>
                )}
                {voiceLoadError && (
                  <p className="text-xs text-[var(--color-error)]">{voiceLoadError}</p>
                )}
              </div>

              {isMistral && (
                <div className="space-y-2 md:col-span-2">
                  <FieldLabel
                    label="Create Voice (Clone)"
                    helpTitle="Voice Cloning"
                    helpDescription="Upload a short audio sample (2–3 seconds minimum) to create a cloned voice via Mistral. The voice will appear in your voice list after creation."
                  />
                  <div className="flex items-center gap-2">
                    <Input
                      placeholder="Voice name"
                      value={voiceCreateName}
                      onChange={(e) => setVoiceCreateName(e.target.value)}
                      className="max-w-xs"
                    />
                    <input
                      ref={voiceFileRef}
                      type="file"
                      accept="audio/*"
                      className="hidden"
                      onChange={handleCreateVoice}
                    />
                    <Button
                      variant="outline"
                      disabled={creatingVoice || !toTrimmed(voiceCreateName)}
                      onClick={() => voiceFileRef.current?.click()}
                    >
                      <Upload className="mr-2 h-4 w-4" />
                      {creatingVoice ? 'Creating...' : 'Upload Sample'}
                    </Button>
                  </div>
                </div>
              )}

              <div className="space-y-2">
                <FieldLabel
                  label="Model"
                  htmlFor="tts-model-id"
                  helpTitle="Model"
                  helpDescription={isElevenLabs
                    ? 'ElevenLabs model used for generation. Multilingual v2 is the default. Turbo and Flash variants trade some quality for lower latency.'
                    : 'Mistral model used for generation. Voxtral Mini TTS is the default zero-shot voice cloning model.'}
                />
                <Select value={formState.modelId} onValueChange={(value) => updateField('modelId', value)}>
                  <SelectTrigger id="tts-model-id">
                    <SelectValue placeholder="Select model" />
                  </SelectTrigger>
                  <SelectContent>
                    {modelOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <FieldLabel
                  label="Locale"
                  htmlFor="tts-locale"
                  helpTitle="Locale"
                  helpDescription="Language and regional accent hint sent to the provider."
                />
                <Select value={formState.locale} onValueChange={(value) => updateField('locale', value)}>
                  <SelectTrigger id="tts-locale">
                    <SelectValue placeholder="Select locale" />
                  </SelectTrigger>
                  <SelectContent>
                    {LOCALE_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <FieldLabel
                  label="Output format"
                  htmlFor="tts-output-format"
                  helpTitle="Output format"
                  helpDescription={isElevenLabs
                    ? 'Audio encoding returned by ElevenLabs. Compressed MP3 is usually best for browser and mobile playback.'
                    : 'Audio format returned by Mistral. PCM offers lowest latency (~0.8s). MP3 is best for general use (~3s).'}
                />
                <Select value={formState.outputFormat} onValueChange={(value) => updateField('outputFormat', value)}>
                  <SelectTrigger id="tts-output-format">
                    <SelectValue placeholder="Select output format" />
                  </SelectTrigger>
                  <SelectContent>
                    {outputFormatOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {isElevenLabs && (
                <>
                  <div className="space-y-2">
                    <FieldLabel
                      label="Streaming latency optimization"
                      htmlFor="tts-optimize-streaming"
                      helpTitle="Streaming latency optimization"
                      helpDescription="ElevenLabs latency tuning from 0 to 4. Higher values return speech faster, but can reduce quality."
                    />
                    <Select value={formState.optimizeStreamingLatency} onValueChange={(value) => updateField('optimizeStreamingLatency', value)}>
                      <SelectTrigger id="tts-optimize-streaming">
                        <SelectValue placeholder="Select latency mode" />
                      </SelectTrigger>
                      <SelectContent>
                        {OPTIMIZE_STREAMING_LATENCY_OPTIONS.map((option) => (
                          <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <FieldLabel
                      label="Stability"
                      htmlFor="tts-stability"
                      helpTitle="Stability"
                      helpDescription="Overrides ElevenLabs voice stability. Lower values create more expressive variation. Higher values create steadier delivery."
                    />
                    <Select value={formState.stability || DEFAULT_SELECT_VALUE} onValueChange={(value) => updateField('stability', value === DEFAULT_SELECT_VALUE ? '' : value)}>
                      <SelectTrigger id="tts-stability">
                        <SelectValue placeholder="Use voice default" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={DEFAULT_SELECT_VALUE}>Use voice default</SelectItem>
                        {UNIT_INTERVAL_OPTIONS.map((option) => (
                          <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <FieldLabel
                      label="Similarity boost"
                      htmlFor="tts-similarity"
                      helpTitle="Similarity boost"
                      helpDescription="Controls how closely the synthesis stays aligned to the original voice characteristics."
                    />
                    <Select value={formState.similarityBoost || DEFAULT_SELECT_VALUE} onValueChange={(value) => updateField('similarityBoost', value === DEFAULT_SELECT_VALUE ? '' : value)}>
                      <SelectTrigger id="tts-similarity">
                        <SelectValue placeholder="Use voice default" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={DEFAULT_SELECT_VALUE}>Use voice default</SelectItem>
                        {UNIT_INTERVAL_OPTIONS.map((option) => (
                          <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Usage Policy</CardTitle>
              <CardDescription>Guardrails applied before synthesis starts.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="tts-max-characters">Max characters per utterance</Label>
                <Input id="tts-max-characters" value={formState.maxCharactersPerUtterance} onChange={(event) => updateField('maxCharactersPerUtterance', event.target.value)} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="tts-max-requests">Max requests per minute per user</Label>
                <Input id="tts-max-requests" value={formState.maxRequestsPerMinutePerUser} onChange={(event) => updateField('maxRequestsPerMinutePerUser', event.target.value)} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="tts-monthly-budget">Monthly character budget</Label>
                <Input id="tts-monthly-budget" value={formState.monthlyCharacterBudget} onChange={(event) => updateField('monthlyCharacterBudget', event.target.value)} placeholder="Optional" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Preview</CardTitle>
              <CardDescription>Validate the current voice configuration against the backend preview endpoint.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="tts-preview-text">Preview text</Label>
                <Textarea
                  id="tts-preview-text"
                  value={formState.previewText}
                  onChange={(event) => updateField('previewText', event.target.value)}
                  rows={4}
                />
              </div>
              <p className="text-xs text-[var(--color-text-tertiary)]">
                Preview validates provider access, stores an AiRun for audit, and plays the synthesized audio in-browser.
              </p>
              {previewBlockedMessage && (
                <p className="text-xs text-[var(--color-text-secondary)]">{previewBlockedMessage}</p>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
