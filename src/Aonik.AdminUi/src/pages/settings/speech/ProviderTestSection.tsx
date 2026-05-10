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

const DEFAULT_TTS_TEXT =
  'Hi, I’m the Payabo voice assistant. This is a quick test of the selected voice.';
const STT_SAMPLE_RATE = 16000;

interface ProviderTestSectionProps {
  providerId: string;
  type: SpeechProviderType;
}

/**
 * Inline "Test" panel rendered at the bottom of the provider edit panel for STT and TTS
 * providers. Reuses the existing AudioWorklet recorder for STT and the native `<audio>`
 * element for TTS playback.
 */
export function ProviderTestSection({ providerId, type }: ProviderTestSectionProps) {
  if (type === 'Tts') return <TtsTestPanel providerId={providerId} />;
  if (type === 'Stt') return <SttTestPanel providerId={providerId} />;
  return null;
}

// ── TTS panel ────────────────────────────────────────────────────────────────

function TtsTestPanel({ providerId }: { providerId: string }) {
  const [text, setText] = useState(DEFAULT_TTS_TEXT);
  // Phase D: voice + model are no longer on the provider config — the test endpoint
  // requires the caller to supply them. Admin types whatever vendor-specific id they
  // want to preview; recipes + chat speech do the same when picking voices.
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
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'TTS test failed.';
      toast.error(message);
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
        <div className="space-y-2">
          <Label htmlFor="tts-test-voice-id">
            Voice id <span className="text-destructive">*</span>
          </Label>
          <Input
            id="tts-test-voice-id"
            value={voiceId}
            onChange={(e) => setVoiceId(e.target.value)}
            placeholder="e.g. alloy / xZP4VGEopzZsZsxXfpyz"
            disabled={busy}
          />
        </div>
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
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'STT test failed.';
      toast.error(message);
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

