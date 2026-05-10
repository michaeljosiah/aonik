import { useRef, useState } from 'react';
import { Loader2, Mic, MicOff, Volume2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { usePcmRecorder } from '@/lib/audio/usePcmRecorder';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import type { SpeechProviderType } from '@/types/speechLibrary';

import { VoicePicker } from './VoicePicker';

const DEFAULT_TTS_TEXT =
  'Hi, I’m the Payabo voice assistant. This is a quick test of the selected voice.';
const STT_SAMPLE_RATE = 16000;

interface ProviderTestSectionProps {
  providerId: string;
  type: SpeechProviderType;
  /** Vendor shortcode — drives the VoicePicker's static / remote / free-text dispatch. */
  vendor: string;
}

/**
 * Inline "Test" panel rendered at the bottom of the provider edit panel for STT and TTS
 * providers. Reuses the existing AudioWorklet recorder for STT and the native `<audio>`
 * element for TTS playback. Phase D: the TTS panel uses the shared VoicePicker so the
 * voice list comes from the vendor catalog (static for OpenAI / Realtime / Voice Live;
 * live API for ElevenLabs + Mistral; free text otherwise).
 */
export function ProviderTestSection({ providerId, type, vendor }: ProviderTestSectionProps) {
  if (type === 'Tts') return <TtsTestPanel providerId={providerId} vendor={vendor} />;
  if (type === 'Stt') return <SttTestPanel providerId={providerId} />;
  return null;
}

// ── TTS panel ────────────────────────────────────────────────────────────────

function TtsTestPanel({ providerId, vendor }: { providerId: string; vendor: string }) {
  const [text, setText] = useState(DEFAULT_TTS_TEXT);
  // Phase D: voice + model are no longer on the provider config — the test endpoint
  // requires the caller to supply them. VoicePicker pulls vendor-specific voice lists.
  const [voiceId, setVoiceId] = useState('');
  const [modelId, setModelId] = useState('');
  const [busy, setBusy] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const handlePlay = async () => {
    if (!text.trim()) {
      toast.error('Enter preview text first.');
      return;
    }
    if (!voiceId.trim()) {
      toast.error('Voice id is required to test TTS.');
      return;
    }
    setBusy(true);
    try {
      const result = await speechProviderLibraryService.testTts(
        providerId,
        text.trim(),
        voiceId.trim(),
        modelId.trim() || null,
      );
      const url = URL.createObjectURL(result.audioBlob);
      if (audioRef.current) audioRef.current.pause();
      const audio = new Audio(url);
      audioRef.current = audio;
      audio.onended = () => URL.revokeObjectURL(url);
      await audio.play();
    } catch (err) {
      // The TTS endpoint returns a Blob on success, so axios's default error path leaves
      // `error.response.data` as a Blob (the JSON error envelope) — `data.error` is
      // therefore undefined and we'd otherwise show a generic "Request failed with
      // status code 400". Read the blob, try to parse the FastEndpoints envelope, and
      // surface the real reason.
      toast.error(await extractErrorMessage(err, 'TTS test failed.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3 rounded-md border bg-muted/20 p-4">
      <div className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
        Test this voice
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <VoicePicker
          id="tts-test-voice-id"
          vendor={vendor}
          value={voiceId}
          onChange={setVoiceId}
          required
          disabled={busy}
        />
        <div className="space-y-2">
          <Label htmlFor="tts-test-model-id">Model override</Label>
          <Input
            id="tts-test-model-id"
            value={modelId}
            onChange={(e) => setModelId(e.target.value)}
            placeholder="leave blank to use provider default"
            disabled={busy}
          />
        </div>
      </div>
      <div className="space-y-2">
        <Label htmlFor="tts-test-text">Preview text</Label>
        <Textarea
          id="tts-test-text"
          rows={2}
          value={text}
          onChange={(e) => setText(e.target.value)}
          disabled={busy}
        />
      </div>
      <Button variant="secondary" onClick={() => void handlePlay()} disabled={busy} size="sm">
        {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Volume2 className="h-4 w-4" />}
        Synthesize and play
      </Button>
    </div>
  );
}

// ── STT panel ────────────────────────────────────────────────────────────────

function SttTestPanel({ providerId }: { providerId: string }) {
  const recorder = usePcmRecorder({ sampleRate: STT_SAMPLE_RATE });
  const [transcribing, setTranscribing] = useState(false);
  const [transcript, setTranscript] = useState<string | null>(null);
  const [language, setLanguage] = useState<string | null>(null);

  const handleStart = async () => {
    setTranscript(null);
    setLanguage(null);
    try {
      await recorder.start();
    } catch {
      toast.error(recorder.error ?? 'Could not start microphone.');
    }
  };

  const handleStop = async () => {
    setTranscribing(true);
    try {
      const pcm = await recorder.stopAndCollect();
      if (!pcm || pcm.length === 0) {
        toast.error('No audio captured. Try again with the mic active.');
        return;
      }
      const audio = new Blob([pcm.buffer as ArrayBuffer], { type: 'audio/pcm' });
      const result = await speechProviderLibraryService.testStt(
        providerId,
        audio,
        recorder.sampleRate ?? STT_SAMPLE_RATE,
      );
      setTranscript(result.text);
      setLanguage(result.language);
      toast.success('Transcription complete.');
    } catch (err) {
      toast.error(await extractErrorMessage(err, 'STT test failed.'));
    } finally {
      setTranscribing(false);
    }
  };

  return (
    <div className="space-y-3 rounded-md border bg-muted/20 p-4">
      <div className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
        Test this transcription engine
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {!recorder.isRecording ? (
          <Button onClick={() => void handleStart()} disabled={transcribing} size="sm">
            <Mic className="h-4 w-4" />
            Start recording
          </Button>
        ) : (
          <Button
            onClick={() => void handleStop()}
            variant="destructive"
            disabled={transcribing}
            size="sm"
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
      </div>

      {recorder.error && <p className="text-xs text-destructive">{recorder.error}</p>}

      {transcript !== null && (
        <div className="space-y-1 rounded-md border bg-background p-3">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <span>Transcription</span>
            {language && <Badge variant="outline">{language}</Badge>}
          </div>
          <p className="text-sm">{transcript}</p>
        </div>
      )}
    </div>
  );
}

// ── Error extraction ──────────────────────────────────────────────────────────

interface FastEndpointsErrorEnvelope {
  message?: string;
  errors?: { generalErrors?: string[] } & Record<string, string[] | undefined>;
}

/**
 * Pull a useful message out of an axios error. Handles three cases:
 *   1. The TTS endpoint uses `responseType: 'blob'`, so even error responses arrive as a
 *      Blob. Read the blob, parse it as JSON, then look for FastEndpoints' standard
 *      `{ message, errors: { generalErrors } }` envelope.
 *   2. JSON error envelope (the STT endpoint and most others) — pluck the same fields
 *      from `error.response.data` directly.
 *   3. Network failure / non-axios error — fall back to `error.message` then to the
 *      provided default.
 */
async function extractErrorMessage(err: unknown, fallback: string): Promise<string> {
  if (err && typeof err === 'object') {
    const response = (err as { response?: { data?: unknown } }).response;
    const data = response?.data;
    if (data instanceof Blob) {
      try {
        const text = await data.text();
        const parsed = JSON.parse(text) as FastEndpointsErrorEnvelope;
        const fromEnvelope = pickFromEnvelope(parsed);
        if (fromEnvelope) return fromEnvelope;
      } catch {
        // not JSON; fall through to the generic axios message
      }
    } else if (data && typeof data === 'object') {
      const fromEnvelope = pickFromEnvelope(data as FastEndpointsErrorEnvelope);
      if (fromEnvelope) return fromEnvelope;
    }
    const message = (err as { message?: string }).message;
    if (message) return message;
  }
  return fallback;
}

function pickFromEnvelope(envelope: FastEndpointsErrorEnvelope): string | null {
  // FastEndpoints' AddError(...) collects under `errors.generalErrors`; prefer those over
  // the generic "One or more errors occurred!" message that always rides along.
  const general = envelope.errors?.generalErrors;
  if (general && general.length > 0) return general.join('; ');
  // Fall through to other field-level errors if any.
  if (envelope.errors) {
    for (const [key, value] of Object.entries(envelope.errors)) {
      if (key === 'generalErrors') continue;
      if (Array.isArray(value) && value.length > 0) return `${key}: ${value.join('; ')}`;
    }
  }
  if (envelope.message && envelope.message !== 'One or more errors occurred!') {
    return envelope.message;
  }
  return null;
}
