import { useEffect, useMemo, useRef, useState } from 'react';
import { Loader2, Volume2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { voiceProviderSettingsService } from '@/services/voiceProviderSettingsService';
import type { VoiceOptionResponse } from '@/types/voice';

const PROVIDERS = ['openai', 'azure', 'elevenlabs', 'mistral'] as const;
type Provider = (typeof PROVIDERS)[number];

const PROVIDER_LABEL: Record<Provider, string> = {
  openai: 'OpenAI',
  azure: 'Azure Speech',
  elevenlabs: 'ElevenLabs',
  mistral: 'Mistral (Voxtral)',
};

const DEFAULT_TEXT =
  'Hi, I’m the Payabo voice assistant. This is a quick test of the selected voice.';

/**
 * Stand-alone TTS testing card. Lets staff pick any supported provider, choose a voice/model,
 * and synthesize a short clip to verify the credential is correct before wiring the provider into
 * the realtime voice pipeline (or other admin pages that use TTS for helper-text playback).
 */
export function TtsTestCard() {
  const [provider, setProvider] = useState<Provider>('openai');
  const [voices, setVoices] = useState<VoiceOptionResponse[]>([]);
  const [voiceId, setVoiceId] = useState<string>('alloy');
  const [modelId, setModelId] = useState<string>('');
  const [region, setRegion] = useState<string>('eastus');
  const [text, setText] = useState<string>(DEFAULT_TEXT);
  const [previewing, setPreviewing] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // Refresh the voice picker whenever provider changes.
  useEffect(() => {
    let cancelled = false;
    void voiceProviderSettingsService.listVoices(provider).then((options) => {
      if (cancelled) return;
      setVoices(options);
      if (options.length > 0 && !options.some((o) => o.id === voiceId)) {
        setVoiceId(options[0].id);
      }
    });
    return () => {
      cancelled = true;
    };
    // Voice list depends only on provider — don't re-fetch on every voiceId change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [provider]);

  // Default model id per provider — can be overridden by typing in the Model input.
  const defaultModelId = useMemo<string>(() => {
    switch (provider) {
      case 'openai':
        return 'tts-1';
      case 'azure':
        return ''; // Azure uses voice + region; model isn't a separate id
      case 'elevenlabs':
        return 'eleven_multilingual_v2';
      case 'mistral':
        return 'voxtral-tts';
    }
  }, [provider]);

  const handlePreview = async () => {
    if (!text.trim()) {
      toast.error('Enter preview text first.');
      return;
    }
    setPreviewing(true);
    try {
      const result = await voiceProviderSettingsService.preview({
        text: text.trim(),
        provider,
        voiceId,
        modelId: modelId.trim() || defaultModelId || null,
        region: provider === 'azure' ? region.trim() : null,
      });
      const url = URL.createObjectURL(result.audioBlob);
      if (audioRef.current) {
        audioRef.current.pause();
      }
      const audio = new Audio(url);
      audioRef.current = audio;
      audio.onended = () => URL.revokeObjectURL(url);
      await audio.play();
    } catch (err) {
      const message =
        (err as { response?: { data?: { errors?: Record<string, string[]> } }; message?: string })
          ?.response?.data?.errors
          ? Object.values(
              (err as { response: { data: { errors: Record<string, string[]> } } }).response.data
                .errors,
            )
              .flat()
              .join(' ')
          : (err as { message?: string })?.message ?? 'TTS preview failed.';
      toast.error(message);
    } finally {
      setPreviewing(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Test text-to-speech</CardTitle>
        <CardDescription>
          Pick any supported TTS provider, supply a voice id, and synthesize a short clip. The
          credential resolver looks up the API key by provider name (OpenAI, Azure, ElevenLabs,
          Mistral) — store keys via the credential card above before testing.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="tts-provider">Provider</Label>
            <Select value={provider} onValueChange={(v) => setProvider(v as Provider)}>
              <SelectTrigger id="tts-provider">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PROVIDERS.map((p) => (
                  <SelectItem key={p} value={p}>
                    {PROVIDER_LABEL[p]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="tts-voice">Voice</Label>
            {voices.length > 0 ? (
              <Select value={voiceId} onValueChange={setVoiceId}>
                <SelectTrigger id="tts-voice">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {voices.map((v) => (
                    <SelectItem key={v.id} value={v.id}>
                      {v.name}
                      {v.description ? ` — ${v.description}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ) : (
              <Input
                id="tts-voice"
                placeholder="Voice id (no presets for this provider)"
                value={voiceId}
                onChange={(e) => setVoiceId(e.target.value)}
              />
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="tts-model">Model id (optional)</Label>
            <Input
              id="tts-model"
              placeholder={defaultModelId || 'Provider default'}
              value={modelId}
              onChange={(e) => setModelId(e.target.value)}
            />
          </div>

          {provider === 'azure' && (
            <div className="space-y-2">
              <Label htmlFor="tts-region">Azure region</Label>
              <Input
                id="tts-region"
                placeholder="eastus"
                value={region}
                onChange={(e) => setRegion(e.target.value)}
              />
            </div>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="tts-text">Preview text</Label>
          <Textarea
            id="tts-text"
            rows={2}
            value={text}
            onChange={(e) => setText(e.target.value)}
            disabled={previewing}
          />
        </div>

        <Button variant="secondary" onClick={() => void handlePreview()} disabled={previewing}>
          {previewing ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Volume2 className="h-4 w-4" />
          )}
          Synthesize and play
        </Button>
      </CardContent>
    </Card>
  );
}
