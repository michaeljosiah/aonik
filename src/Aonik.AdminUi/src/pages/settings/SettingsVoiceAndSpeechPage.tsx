import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle, AudioLines, CheckCircle2, Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { voiceProviderSettingsService } from '@/services/voiceProviderSettingsService';
import type {
  VoiceOptionResponse,
  VoiceProviderCredentialResponse,
  VoiceProviderSettingsResponse,
  VoiceRecipeResponse,
} from '@/types/voice';

import { PipelineTestCard } from './voice/PipelineTestCard';
import { SttTestCard } from './voice/SttTestCard';
import { TtsTestCard } from './voice/TtsTestCard';

function ensureChained(settings: VoiceProviderSettingsResponse): VoiceProviderSettingsResponse {
  if (settings.chained) return settings;
  return {
    ...settings,
    chained: {
      stt: { vendor: 'openai-whisper', model: 'whisper-1' },
      tts: { vendor: 'openai', voiceId: 'alloy', modelId: 'tts-1' },
      vad: { kind: 'energy', stopMs: 800 },
      transcriptionFilter: true,
      sentenceAggregator: true,
    },
  };
}

function statusBadgeForCredential(snapshot: VoiceProviderCredentialResponse | null) {
  if (!snapshot) return null;
  if (snapshot.hasTenantOverride) {
    return (
      <Badge variant="default" className="gap-1">
        <CheckCircle2 className="h-3 w-3" /> Tenant override
      </Badge>
    );
  }
  if (snapshot.hasHostCredential) {
    return (
      <Badge variant="secondary" className="gap-1">
        <CheckCircle2 className="h-3 w-3" /> Host default
      </Badge>
    );
  }
  if (snapshot.effectiveSource === 'Configuration') {
    return (
      <Badge variant="outline" className="gap-1">
        <CheckCircle2 className="h-3 w-3" /> Configuration fallback
      </Badge>
    );
  }
  return (
    <Badge variant="error" className="gap-1">
      <AlertCircle className="h-3 w-3" /> Not configured
    </Badge>
  );
}

export function SettingsVoiceAndSpeechPage() {
  const [settings, setSettings] = useState<VoiceProviderSettingsResponse | null>(null);
  const [recipes, setRecipes] = useState<VoiceRecipeResponse[]>([]);
  const [voices, setVoices] = useState<VoiceOptionResponse[]>([]);
  const [credential, setCredential] = useState<VoiceProviderCredentialResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [saving, setSaving] = useState(false);
  const [savingCredential, setSavingCredential] = useState(false);
  const [apiKeyInput, setApiKeyInput] = useState('');

  const ttsVendor = settings?.chained?.tts.vendor ?? 'openai';
  const credentialProvider = useMemo(() => {
    // The Settings tab pivots on the realtime-pipeline TTS vendor since that's the user-visible
    // voice. Independent multi-provider credential management lives in the Test TTS / Test STT
    // tabs (each provider has its own resolver key).
    return ttsVendor.toLowerCase().startsWith('openai') ? 'OpenAI' : ttsVendor;
  }, [ttsVendor]);

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [s, r] = await Promise.all([
        voiceProviderSettingsService.get(),
        voiceProviderSettingsService.listRecipes(),
      ]);
      const normalized = ensureChained(s);
      setSettings(normalized);
      setRecipes(r);
    } catch (err) {
      setError('Failed to load voice settings.');
      // eslint-disable-next-line no-console
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  useEffect(() => {
    if (!settings?.chained) return;
    const vendor = settings.chained.tts.vendor;
    void voiceProviderSettingsService
      .listVoices(vendor)
      .then(setVoices)
      .catch(() => setVoices([]));
  }, [settings?.chained?.tts.vendor]);

  useEffect(() => {
    void voiceProviderSettingsService
      .getCredential(credentialProvider)
      .then(setCredential)
      .catch(() => setCredential(null));
  }, [credentialProvider]);

  const updateSetting = (next: VoiceProviderSettingsResponse) => {
    setSettings(ensureChained(next));
  };

  const handleRecipeSelect = (recipe: VoiceRecipeResponse) => {
    if (!recipe.implemented) {
      toast.info(`${recipe.name} is reserved for v1.1 and not yet wired.`);
      return;
    }
    updateSetting({ ...recipe.settings, enabled: settings?.enabled ?? true });
  };

  const handleEnabledToggle = (enabled: boolean) => {
    if (!settings) return;
    updateSetting({ ...settings, enabled });
  };

  const handleVoiceChange = (voiceId: string) => {
    if (!settings?.chained) return;
    updateSetting({
      ...settings,
      chained: {
        ...settings.chained,
        tts: { ...settings.chained.tts, voiceId },
      },
    });
  };

  const handleSave = async () => {
    if (!settings) return;
    setSaving(true);
    try {
      const saved = await voiceProviderSettingsService.update(settings);
      setSettings(ensureChained(saved));
      toast.success('Voice settings saved.');
    } catch (err) {
      const message = (err as { message?: string })?.message ?? 'Failed to save voice settings.';
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const handleSaveCredential = async (clear = false) => {
    setSavingCredential(true);
    try {
      const updated = await voiceProviderSettingsService.updateCredential(credentialProvider, {
        apiKey: clear ? null : apiKeyInput,
        clearStoredValue: clear,
      });
      setCredential(updated);
      setApiKeyInput('');
      toast.success(clear ? 'Voice credential cleared.' : 'Voice credential saved.');
    } catch (err) {
      const message = (err as { message?: string })?.message ?? 'Failed to update credential.';
      toast.error(message);
    } finally {
      setSavingCredential(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading voice settings…
      </div>
    );
  }

  if (error || !settings) {
    return (
      <div className="p-6">
        <Card>
          <CardContent className="flex items-center gap-2 p-6 text-destructive">
            <AlertCircle className="h-4 w-4" />
            {error ?? 'Voice settings unavailable.'}
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-start gap-3">
        <AudioLines className="mt-1 h-6 w-6 text-primary" />
        <div>
          <h1 className="text-xl font-semibold">Voice &amp; Speech</h1>
          <p className="text-sm text-muted-foreground">
            Configure real-time voice mode for the Payabo mobile app and validate each component
            (STT, TTS, full pipeline) before rolling out.
          </p>
        </div>
      </div>

      <Tabs defaultValue="settings">
        <TabsList>
          <TabsTrigger value="settings">Settings</TabsTrigger>
          <TabsTrigger value="tts">Test TTS</TabsTrigger>
          <TabsTrigger value="stt">Test STT</TabsTrigger>
          <TabsTrigger value="pipeline">Live pipeline</TabsTrigger>
        </TabsList>

        {/* ── Settings tab — recipe + voice picker + credential + save ────────────── */}
        <TabsContent value="settings" className="space-y-6">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Realtime voice mode</CardTitle>
                  <CardDescription>
                    Pick a recipe, configure the provider credential, and save. Use the test tabs
                    above to verify each component works end-to-end.
                  </CardDescription>
                </div>
                <div className="flex items-center gap-3">
                  <Label htmlFor="voice-enabled" className="text-sm">
                    {settings.enabled ? 'Enabled' : 'Disabled'}
                  </Label>
                  <Switch
                    id="voice-enabled"
                    checked={settings.enabled}
                    onCheckedChange={handleEnabledToggle}
                  />
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-6">
              {/* Recipe picker */}
              <div className="space-y-3">
                <Label className="text-sm font-medium">Recipe</Label>
                <div className="grid gap-3 md:grid-cols-2">
                  {recipes.map((recipe) => {
                    const selected = settings.recipeId === recipe.id;
                    return (
                      <button
                        type="button"
                        key={recipe.id}
                        onClick={() => handleRecipeSelect(recipe)}
                        disabled={!recipe.implemented}
                        className={`flex flex-col gap-2 rounded-md border p-4 text-left transition ${
                          selected
                            ? 'border-primary ring-2 ring-primary/30'
                            : 'border-border hover:border-primary/40'
                        } ${recipe.implemented ? '' : 'cursor-not-allowed opacity-60'}`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="font-medium">{recipe.name}</div>
                          <div className="flex gap-1">
                            <Badge variant="outline">{recipe.costRanking}</Badge>
                            <Badge variant="outline">{recipe.latencyTarget}</Badge>
                          </div>
                        </div>
                        <p className="text-xs text-muted-foreground">{recipe.description}</p>
                        {!recipe.implemented && (
                          <Badge variant="secondary" className="w-fit">
                            Coming in v1.1
                          </Badge>
                        )}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Voice picker */}
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="voice-id">Voice</Label>
                  <Select
                    value={settings.chained?.tts.voiceId ?? 'alloy'}
                    onValueChange={handleVoiceChange}
                  >
                    <SelectTrigger id="voice-id">
                      <SelectValue placeholder="Select a voice" />
                    </SelectTrigger>
                    <SelectContent>
                      {voices.map((voice) => (
                        <SelectItem key={voice.id} value={voice.id}>
                          {voice.name}
                          {voice.description ? ` — ${voice.description}` : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Provider</Label>
                  <div className="flex h-9 items-center rounded-md border bg-muted px-3 text-sm">
                    {settings.chained?.tts.vendor ?? 'openai'} ·{' '}
                    {settings.chained?.tts.modelId ?? 'tts-1'}
                  </div>
                </div>
              </div>

              {/* Credential entry */}
              <div className="space-y-2 rounded-md border p-4">
                <div className="flex items-center justify-between">
                  <Label htmlFor="voice-api-key" className="text-sm font-medium">
                    {credentialProvider} API key
                  </Label>
                  {statusBadgeForCredential(credential)}
                </div>
                <p className="text-xs text-muted-foreground">
                  Stored encrypted at rest. The status badge above reflects whether a tenant
                  override, host default, or configuration fallback is in effect.
                </p>
                <div className="flex gap-2">
                  <Input
                    id="voice-api-key"
                    type="password"
                    placeholder={
                      credential?.hasTenantOverride
                        ? 'Tenant override is set. Enter a new key to replace it.'
                        : 'sk-…'
                    }
                    value={apiKeyInput}
                    onChange={(e) => setApiKeyInput(e.target.value)}
                    disabled={savingCredential}
                  />
                  <Button
                    onClick={() => void handleSaveCredential(false)}
                    disabled={savingCredential || !apiKeyInput.trim()}
                  >
                    {savingCredential ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Save className="h-4 w-4" />
                    )}
                    Save
                  </Button>
                  {credential?.hasTenantOverride && (
                    <Button
                      variant="outline"
                      onClick={() => void handleSaveCredential(true)}
                      disabled={savingCredential}
                    >
                      Clear
                    </Button>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Persist changes */}
          <div className="flex items-center justify-end gap-2">
            <Button variant="outline" onClick={() => void loadAll()} disabled={saving}>
              Discard changes
            </Button>
            <Button onClick={() => void handleSave()} disabled={saving}>
              {saving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save voice settings
            </Button>
          </div>

          {/* Chat speech (TTS) link — v1 keeps the existing TTS page intact. */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Chat speech (text-to-speech)</CardTitle>
              <CardDescription>
                Voice mode covers the realtime conversation experience. The existing chat speech /
                text-to-speech configuration remains on its own page until v1.1 unifies the two.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Button asChild variant="outline">
                <a href="/settings/text-to-speech">Open chat speech settings</a>
              </Button>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Test TTS tab — multi-provider synthesis check ─────────────────────── */}
        <TabsContent value="tts">
          <TtsTestCard />
        </TabsContent>

        {/* ── Test STT tab — mic capture + transcription ────────────────────────── */}
        <TabsContent value="stt">
          <SttTestCard />
        </TabsContent>

        {/* ── Live pipeline tab — full WSS round-trip ───────────────────────────── */}
        <TabsContent value="pipeline">
          <PipelineTestCard />
        </TabsContent>
      </Tabs>
    </div>
  );
}
