import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, AudioLines, RefreshCw, Save, Volume2, Waves } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { getSelectedTenant } from '@/lib/tenantContext';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';
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

function isForbiddenError(error: unknown) {
  if (!error || typeof error !== 'object' || !('response' in error)) {
    return false;
  }

  const response = (error as { response?: { status?: number } }).response;
  return response?.status === 403;
}

function buildFormState(snapshot: TextToSpeechSettingsResponse): TextToSpeechFormState {
  return {
    enabled: snapshot.enabled,
    fallbackToNativeOnFailure: snapshot.fallbackToNativeOnFailure,
    provider: toInputValue(snapshot.defaultProfile.provider) || 'ElevenLabs',
    voiceId: toInputValue(snapshot.defaultProfile.voiceId),
    modelId: toInputValue(snapshot.defaultProfile.modelId) || 'eleven_multilingual_v2',
    locale: toInputValue(snapshot.defaultProfile.locale) || 'en-US',
    outputFormat: toInputValue(snapshot.defaultProfile.outputFormat) || 'mp3_44100_128',
    optimizeStreamingLatency: toInputValue(snapshot.defaultProfile.providerOptions.optimizeStreamingLatency) || '3',
    stability: toInputValue(snapshot.defaultProfile.providerOptions.stability),
    similarityBoost: toInputValue(snapshot.defaultProfile.providerOptions.similarityBoost),
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
  return {
    enabled: formState.enabled,
    fallbackToNativeOnFailure: formState.fallbackToNativeOnFailure,
    defaultProfile: {
      provider: toTrimmed(formState.provider) || 'ElevenLabs',
      voiceId: toTrimmed(formState.voiceId),
      modelId: toTrimmed(formState.modelId) || null,
      locale: toTrimmed(formState.locale) || null,
      outputFormat: toTrimmed(formState.outputFormat) || null,
      providerOptions: {
        optimizeStreamingLatency: toTrimmed(formState.optimizeStreamingLatency) || null,
        stability: toTrimmed(formState.stability) || null,
        similarityBoost: toTrimmed(formState.similarityBoost) || null,
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
  const [hostCredentialAccessDenied, setHostCredentialAccessDenied] = useState(false);
  const [voiceLoadError, setVoiceLoadError] = useState<string | null>(null);

  const activeVoice = useMemo(
    () => voices.find((voice) => voice.voiceId === formState?.voiceId) ?? null,
    [voices, formState?.voiceId],
  );
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

  const loadSettings = async () => {
    setLoading(true);
    setError(null);

    try {
      const [snapshot, hostCredentialSnapshot, tenantCredentialSnapshot] = await Promise.all([
        textToSpeechSettingsService.get(),
        textToSpeechSettingsService.getHostCredential().catch((err: unknown) => {
          if (isForbiddenError(err)) {
            return null;
          }

          throw err;
        }),
        textToSpeechSettingsService.getTenantCredential(),
      ]);
      const nextState = buildFormState(snapshot);
      setFormState(nextState);
      setHostCredential(hostCredentialSnapshot);
      setTenantCredential(tenantCredentialSnapshot);
      setHostCredentialAccessDenied(hostCredentialSnapshot == null);
      await loadVoices(nextState.provider);
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to load text-to-speech settings.'));
    } finally {
      setLoading(false);
    }
  };

  const handleSaveHostCredential = async (clearStoredValue = false) => {
    if (!formState) {
      return;
    }

    setSavingHostCredential(true);
    setError(null);

    try {
      const snapshot = await textToSpeechSettingsService.updateHostCredential({
        provider: formState.provider || 'ElevenLabs',
        apiKey: clearStoredValue ? null : toTrimmed(formState.hostApiKey) || null,
        clearStoredValue,
      });
      const tenantSnapshot = await textToSpeechSettingsService.getTenantCredential();

      setHostCredential(snapshot);
      setTenantCredential(tenantSnapshot);
      updateField('hostApiKey', '');
      await loadVoices(formState.provider || 'ElevenLabs');
      toast.success(clearStoredValue ? 'Host ElevenLabs key cleared.' : 'Host ElevenLabs key saved.');
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to update host ElevenLabs key.');
      setError(message);
      toast.error(message);
    } finally {
      setSavingHostCredential(false);
    }
  };

  const handleSaveTenantCredential = async (clearStoredValue = false) => {
    if (!formState) {
      return;
    }

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
      toast.success(clearStoredValue ? 'Tenant ElevenLabs override cleared.' : 'Tenant ElevenLabs override saved.');
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to update tenant ElevenLabs key.');
      setError(message);
      toast.error(message);
    } finally {
      setSavingTenantCredential(false);
    }
  };

  useEffect(() => {
    void loadSettings();
  }, []);

  useEffect(() => {
    return () => {
      if (previewAudio) {
        previewAudio.pause();
        previewAudio.src = '';
      }
    };
  }, [previewAudio]);

  useEffect(() => {
    if (!formState?.provider) {
      return;
    }

    let active = true;
    void textToSpeechSettingsService.listVoices(formState.provider)
      .then((items) => {
        if (active) {
          setVoiceLoadError(null);
          setVoices(items);
        }
      })
      .catch((err: unknown) => {
        if (active) {
          setVoiceLoadError(resolveUserMessage(err, 'Failed to load voices.'));
          setVoices([]);
        }
      });

    return () => {
      active = false;
    };
  }, [formState?.provider]);

  const handleSave = async () => {
    if (!formState) {
      return;
    }

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
    if (!formState) {
      return;
    }

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

  const showHostCredentialCard = !hostCredentialAccessDenied;

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
            Configure host and tenant ElevenLabs credentials, tenant voice playback, fallback behavior, and usage limits.
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
                      <CardDescription>Default ElevenLabs API key used when a tenant does not provide an override.</CardDescription>
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
                      <Label htmlFor="tts-host-api-key">ElevenLabs API Key (update only)</Label>
                      <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">Platform.TextToSpeech.Providers.ElevenLabs.ApiKey</span>
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
                    <CardDescription>Optional ElevenLabs API key override for the currently selected tenant.</CardDescription>
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
                    <Label htmlFor="tts-tenant-api-key">Tenant ElevenLabs API Key (update only)</Label>
                    <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">Platform.TextToSpeech.Providers.ElevenLabs.ApiKey</span>
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
                <Label>Provider</Label>
                <Select value={formState.provider} onValueChange={(value) => updateField('provider', value)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select provider" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="ElevenLabs">ElevenLabs</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Voice</Label>
                <Select value={formState.voiceId} onValueChange={(value) => updateField('voiceId', value)}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select voice" />
                  </SelectTrigger>
                  <SelectContent>
                    {voices.map((voice) => (
                      <SelectItem key={voice.voiceId} value={voice.voiceId}>
                        {voice.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {activeVoice && (
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    {activeVoice.category ?? 'General'}
                    {activeVoice.labels.gender ? ` • ${activeVoice.labels.gender}` : ''}
                    {activeVoice.labels.accent ? ` • ${activeVoice.labels.accent}` : ''}
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

              <div className="space-y-2">
                <Label htmlFor="tts-model-id">Model ID</Label>
                <Input id="tts-model-id" value={formState.modelId} onChange={(event) => updateField('modelId', event.target.value)} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="tts-locale">Locale</Label>
                <Input id="tts-locale" value={formState.locale} onChange={(event) => updateField('locale', event.target.value)} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="tts-output-format">Output format</Label>
                <Input id="tts-output-format" value={formState.outputFormat} onChange={(event) => updateField('outputFormat', event.target.value)} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="tts-optimize-streaming">Optimize streaming latency</Label>
                <Input id="tts-optimize-streaming" value={formState.optimizeStreamingLatency} onChange={(event) => updateField('optimizeStreamingLatency', event.target.value)} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="tts-stability">Stability</Label>
                <Input id="tts-stability" value={formState.stability} onChange={(event) => updateField('stability', event.target.value)} />
              </div>

              <div className="space-y-2">
                <Label htmlFor="tts-similarity">Similarity boost</Label>
                <Input id="tts-similarity" value={formState.similarityBoost} onChange={(event) => updateField('similarityBoost', event.target.value)} />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Usage Policy</CardTitle>
              <CardDescription>Guardrails applied before ElevenLabs synthesis starts.</CardDescription>
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
                Preview validates provider access, stores an `AiRun` for audit, and plays the synthesized audio in-browser.
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
