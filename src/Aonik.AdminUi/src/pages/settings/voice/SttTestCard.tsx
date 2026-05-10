import { useState } from 'react';
import { AlertCircle, Loader2, Mic, MicOff } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
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
import { usePcmRecorder } from '@/lib/audio/usePcmRecorder';
import { voiceProviderSettingsService } from '@/services/voiceProviderSettingsService';

const PROVIDERS = ['openai-whisper', 'azure'] as const;
type Provider = (typeof PROVIDERS)[number];

const PROVIDER_LABEL: Record<Provider, string> = {
  'openai-whisper': 'OpenAI Whisper',
  azure: 'Azure Speech',
};

const SAMPLE_RATE = 16000; // Whisper + Azure both accept 16 kHz natively.

/**
 * Stand-alone STT testing card. Captures a short mic clip from the admin and ships it as 16-bit
 * PCM to the chosen STT provider so the admin can validate credential, language, and (Azure)
 * region before wiring the provider into the realtime voice pipeline.
 */
export function SttTestCard() {
  const [provider, setProvider] = useState<Provider>('openai-whisper');
  const [model, setModel] = useState<string>(''); // optional, default per provider
  const [language, setLanguage] = useState<string>('');
  const [region, setRegion] = useState<string>('eastus');
  const [transcribing, setTranscribing] = useState(false);
  const [transcript, setTranscript] = useState<string | null>(null);
  const [detectedLanguage, setDetectedLanguage] = useState<string | null>(null);

  const recorder = usePcmRecorder({ sampleRate: SAMPLE_RATE });

  const handleStart = async () => {
    setTranscript(null);
    setDetectedLanguage(null);
    try {
      await recorder.start();
    } catch {
      toast.error(recorder.error ?? 'Could not start microphone.');
    }
  };

  const handleStopAndTranscribe = async () => {
    setTranscribing(true);
    try {
      const pcm = await recorder.stopAndCollect();
      if (!pcm || pcm.length === 0) {
        toast.error('No audio captured. Try again with the mic active.');
        return;
      }

      // Cast to ArrayBuffer — pcm.buffer is typed as ArrayBufferLike (which includes
      // SharedArrayBuffer) but Int16Array allocated locally is always backed by ArrayBuffer.
      const audio = new Blob([pcm.buffer as ArrayBuffer], { type: 'audio/pcm' });
      const result = await voiceProviderSettingsService.previewStt({
        audio,
        provider,
        model: model.trim() || null,
        language: language.trim() || null,
        region: provider === 'azure' ? region.trim() : null,
        sampleRate: recorder.sampleRate ?? SAMPLE_RATE,
      });
      setTranscript(result.text);
      setDetectedLanguage(result.language);
      toast.success('Transcription complete.');
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
          : (err as { message?: string })?.message ?? 'STT preview failed.';
      toast.error(message);
    } finally {
      setTranscribing(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Test speech-to-text</CardTitle>
        <CardDescription>
          Record a short mic clip and transcribe it via the chosen STT provider. Validates the
          credential, language, and (Azure only) region before you wire the provider into the
          realtime voice pipeline.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="stt-provider">Provider</Label>
            <Select value={provider} onValueChange={(v) => setProvider(v as Provider)}>
              <SelectTrigger id="stt-provider">
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
            <Label htmlFor="stt-model">Model (optional)</Label>
            <Input
              id="stt-model"
              placeholder={provider === 'openai-whisper' ? 'whisper-1' : ''}
              value={model}
              onChange={(e) => setModel(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="stt-language">Language (BCP-47, optional)</Label>
            <Input
              id="stt-language"
              placeholder={provider === 'azure' ? 'en-US' : 'en (auto-detect if blank)'}
              value={language}
              onChange={(e) => setLanguage(e.target.value)}
            />
          </div>

          {provider === 'azure' && (
            <div className="space-y-2">
              <Label htmlFor="stt-region">Azure region</Label>
              <Input
                id="stt-region"
                placeholder="eastus"
                value={region}
                onChange={(e) => setRegion(e.target.value)}
              />
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {!recorder.isRecording ? (
            <Button onClick={() => void handleStart()} disabled={transcribing}>
              <Mic className="h-4 w-4" />
              Start recording
            </Button>
          ) : (
            <Button
              onClick={() => void handleStopAndTranscribe()}
              variant="destructive"
              disabled={transcribing}
            >
              {transcribing ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <MicOff className="h-4 w-4" />
              )}
              Stop and transcribe
            </Button>
          )}
          {recorder.isRecording && (
            <Badge variant="error" className="animate-pulse">
              Recording…
            </Badge>
          )}
          {recorder.sampleRate && (
            <span className="text-xs text-muted-foreground">
              Capture rate: {recorder.sampleRate} Hz
            </span>
          )}
        </div>

        {recorder.error && (
          <div className="flex items-start gap-2 rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
            <AlertCircle className="mt-0.5 h-4 w-4" />
            <span>{recorder.error}</span>
          </div>
        )}

        {transcript !== null && (
          <div className="space-y-1 rounded-md border p-3">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span>Transcription</span>
              {detectedLanguage && <Badge variant="outline">{detectedLanguage}</Badge>}
            </div>
            <p className="text-sm">{transcript}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
