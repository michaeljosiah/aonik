import { useRef, useState } from "react";
import { Loader2, Mic, MicOff, Square, Volume2 } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { usePcmRecorder } from "@/lib/audio/usePcmRecorder";
import { speechProviderLibraryService } from "@/services/speechProviderLibraryService";
import type { SpeechProviderType } from "@/types/speechLibrary";

import { extractAudioApiError } from "./_audioApiError";
import { ModelPicker } from "./ModelPicker";
import { VoicePicker } from "./VoicePicker";

const DEFAULT_TTS_TEXT =
  "Hi, I’m the Payabo voice assistant. This is a quick test of the selected voice.";
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
export function ProviderTestSection({
  providerId,
  type,
  vendor,
}: ProviderTestSectionProps) {
  if (type === "Tts")
    return <TtsTestPanel providerId={providerId} vendor={vendor} />;
  if (type === "Stt") return <SttTestPanel providerId={providerId} />;
  return null;
}

// ── TTS panel ────────────────────────────────────────────────────────────────

function TtsTestPanel({
  providerId,
  vendor,
}: {
  providerId: string;
  vendor: string;
}) {
  const [text, setText] = useState(DEFAULT_TTS_TEXT);
  // Phase D: voice + model are no longer on the provider config — the test endpoint
  // requires the caller to supply them. VoicePicker / ModelPicker pull vendor-specific
  // catalogs (static for OpenAI / Realtime; live API for ElevenLabs + Mistral voices).
  const [voiceId, setVoiceId] = useState("");
  const [modelId, setModelId] = useState("");
  const [busy, setBusy] = useState(false);
  // Track playback so the button toggles between Synthesize and Stop. `audioRef` already
  // held the live audio element for the "pause previous before starting next" hack;
  // adding a Stop button just exposes that capability to the user explicitly.
  const [playing, setPlaying] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);

  const cleanupAudio = () => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.src = "";
      audioRef.current = null;
    }
    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current);
      audioUrlRef.current = null;
    }
    setPlaying(false);
  };

  const handleStop = () => {
    cleanupAudio();
  };

  const handlePlay = async () => {
    if (!text.trim()) {
      toast.error("Enter preview text first.");
      return;
    }
    if (!voiceId.trim()) {
      toast.error("Voice id is required to test TTS.");
      return;
    }
    cleanupAudio();
    setBusy(true);
    try {
      const result = await speechProviderLibraryService.testTts(
        providerId,
        text.trim(),
        voiceId.trim(),
        modelId.trim() || null,
      );
      const url = URL.createObjectURL(result.audioBlob);
      audioUrlRef.current = url;
      const audio = new Audio(url);
      audioRef.current = audio;
      audio.onended = () => cleanupAudio();
      audio.onerror = () => {
        cleanupAudio();
        toast.error(
          "Audio playback failed. The provider may have returned an unsupported format.",
        );
      };
      setPlaying(true);
      await audio.play();
    } catch (err) {
      // The TTS endpoint returns a Blob on success, so axios's default error path leaves
      // `error.response.data` as a Blob (the JSON error envelope) — `data.error` is
      // therefore undefined and we'd otherwise show a generic "Request failed with
      // status code 400". Read the blob, try to parse the FastEndpoints envelope, and
      // surface the real reason.
      cleanupAudio();
      toast.error(await extractAudioApiError(err, "TTS test failed."));
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
        <ModelPicker
          id="tts-test-model-id"
          vendor={vendor}
          value={modelId}
          onChange={setModelId}
          disabled={busy}
        />
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
      <div className="flex items-center gap-2">
        {playing ? (
          // Stop is destructive-ish — colour it like the recorder's Stop button so admins
          // immediately understand it cancels the in-flight playback.
          <Button variant="destructive" onClick={handleStop} size="sm">
            <Square className="h-4 w-4" />
            Stop
          </Button>
        ) : (
          <Button
            variant="secondary"
            onClick={() => void handlePlay()}
            disabled={busy}
            size="sm"
          >
            {busy ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Volume2 className="h-4 w-4" />
            )}
            {busy ? "Synthesising…" : "Synthesize and play"}
          </Button>
        )}
        {playing && (
          <Badge variant="outline" className="animate-pulse">
            Playing
          </Badge>
        )}
      </div>
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
      toast.error(recorder.error ?? "Could not start microphone.");
    }
  };

  const handleStop = async () => {
    setTranscribing(true);
    try {
      const pcm = await recorder.stopAndCollect();
      if (!pcm || pcm.length === 0) {
        toast.error("No audio captured. Try again with the mic active.");
        return;
      }
      const audio = new Blob([pcm.buffer as ArrayBuffer], {
        type: "audio/pcm",
      });
      const result = await speechProviderLibraryService.testStt(
        providerId,
        audio,
        recorder.sampleRate ?? STT_SAMPLE_RATE,
      );
      setTranscript(result.text);
      setLanguage(result.language);
      toast.success("Transcription complete.");
    } catch (err) {
      toast.error(await extractAudioApiError(err, "STT test failed."));
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
          <Button
            onClick={() => void handleStart()}
            disabled={transcribing}
            size="sm"
          >
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

      {recorder.error && (
        <p className="text-xs text-destructive">{recorder.error}</p>
      )}

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
