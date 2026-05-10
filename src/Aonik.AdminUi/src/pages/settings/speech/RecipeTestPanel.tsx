import { useEffect, useRef, useState } from "react";
import {
  CircleAlert,
  Layers,
  Loader2,
  Mic,
  MicOff,
  Radio,
  Square,
  Volume2,
} from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { SheetBody, SheetFooter, SheetHeader } from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { usePcmRecorder } from "@/lib/audio/usePcmRecorder";
import { speechProviderLibraryService } from "@/services/speechProviderLibraryService";
import type { SpeechProvider } from "@/types/speechLibrary";
import type { VoiceRecipe } from "@/types/voiceRecipes";

import { extractAudioApiError } from "./_audioApiError";

/**
 * Recipe-level test panel (spec 024 Phase E). Renders inside a Sheet from the Recipes list.
 *
 * <para>
 * Two flavours:
 * <list type="bullet">
 *   <item>
 *     <description>
 *       <strong>Chained</strong> — runs the recipe's TTS leg (vendor + voice + model from the
 *       recipe body) and STT leg independently. Same wire calls as the inline provider tests,
 *       but the inputs are pinned to the recipe so admins can't accidentally test the wrong
 *       voice. Two cards stack vertically: TTS on top, STT below.
 *     </description>
 *   </item>
 *   <item>
 *     <description>
 *       <strong>Composite</strong> — surfaces a "live test ships with the WSS phase" message
 *       since composite recipes (Voice Live / OpenAI Realtime) need a bidirectional WebSocket
 *       round-trip the admin UI doesn't have wired yet. Phase G follow-up.
 *     </description>
 *   </item>
 * </list>
 * </para>
 *
 * <para>
 * Implementation notes: deliberately self-contained — the provider test panel
 * (<c>ProviderTestSection</c>) had pickers for voice/model that we don't want here, and the
 * locked-down recipe context makes the UX simpler (no "did the user pick the right voice?"
 * ambiguity). The error envelope helper is shared; everything else is bespoke for the recipe
 * affordance.
 * </para>
 */

const STT_SAMPLE_RATE = 16000;
const DEFAULT_TTS_TEXT =
  "Hi, I’m the Payabo voice assistant. This is a quick recipe smoke test.";

interface RecipeTestPanelProps {
  recipe: VoiceRecipe;
  /** Provider library so we can resolve provider id → display name + vendor for the badges. */
  providers: SpeechProvider[];
  onClose: () => void;
}

export function RecipeTestPanel({
  recipe,
  providers,
  onClose,
}: RecipeTestPanelProps) {
  const sttProvider =
    recipe.kind === "Chained" && recipe.chained
      ? (providers.find((p) => p.id === recipe.chained!.sttProviderId) ?? null)
      : null;
  const ttsProvider =
    recipe.kind === "Chained" && recipe.chained
      ? (providers.find((p) => p.id === recipe.chained!.ttsProviderId) ?? null)
      : null;
  const compositeProvider =
    recipe.kind === "Composite" && recipe.composite
      ? (providers.find(
          (p) => p.id === recipe.composite!.compositeProviderId,
        ) ?? null)
      : null;

  return (
    <>
      <SheetHeader
        icon={
          recipe.kind === "Composite" ? (
            <Radio className="h-4 w-4" />
          ) : (
            <Layers className="h-4 w-4" />
          )
        }
        title={`Test "${recipe.displayName}"`}
        subtitle={
          recipe.kind === "Composite"
            ? "Composite recipes need the live WSS round-trip — surfaced as a status panel for now."
            : "Run the TTS and STT legs independently to validate provider credentials and voice picks."
        }
      />
      <SheetBody className="gap-5">
        {recipe.kind === "Chained" ? (
          <>
            {ttsProvider ? (
              <ChainedTtsCard
                providerId={ttsProvider.id}
                providerName={ttsProvider.displayName}
                vendor={ttsProvider.vendor}
                voiceId={recipe.chained?.ttsVoiceId ?? ""}
                modelId={recipe.chained?.ttsModelId ?? null}
              />
            ) : (
              <MissingProviderCard label="TTS provider" />
            )}
            {sttProvider ? (
              <ChainedSttCard
                providerId={sttProvider.id}
                providerName={sttProvider.displayName}
                vendor={sttProvider.vendor}
                modelId={recipe.chained?.sttModel ?? null}
                language={recipe.chained?.sttLanguage ?? null}
              />
            ) : (
              <MissingProviderCard label="STT provider" />
            )}
          </>
        ) : (
          <CompositeStatusCard
            providerName={compositeProvider?.displayName ?? "Unknown provider"}
            vendor={compositeProvider?.vendor ?? "—"}
            voice={recipe.composite?.voice ?? "—"}
            model={recipe.composite?.model ?? null}
          />
        )}
      </SheetBody>
      <SheetFooter className="justify-end">
        <Button variant="outline" size="sm" onClick={onClose}>
          Close
        </Button>
      </SheetFooter>
    </>
  );
}

// ── TTS card (chained) ───────────────────────────────────────────────────────

function ChainedTtsCard({
  providerId,
  providerName,
  vendor,
  voiceId,
  modelId,
}: {
  providerId: string;
  providerName: string;
  vendor: string;
  voiceId: string;
  modelId: string | null;
}) {
  const [text, setText] = useState(DEFAULT_TTS_TEXT);
  const [busy, setBusy] = useState(false);
  const [playing, setPlaying] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);

  const cleanupAudio = () => {
    if (audioRef.current) {
      // Detach listeners BEFORE clearing src — assigning `src = ''` makes the
      // browser fire an `error` event (it now has no source), which would
      // otherwise trip our onerror handler and toast a spurious "playback
      // failed" message even after a successful end-of-track cleanup.
      audioRef.current.onended = null;
      audioRef.current.onerror = null;
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

  // Tear down on unmount so a sheet close mid-playback doesn't leave audio running.
  useEffect(() => () => cleanupAudio(), []);

  const handlePlay = async () => {
    if (!text.trim()) {
      toast.error("Enter preview text first.");
      return;
    }
    if (!voiceId.trim()) {
      toast.error(
        "Recipe is missing a TTS voice id — open the recipe editor and set one.",
      );
      return;
    }
    cleanupAudio();
    setBusy(true);
    try {
      const result = await speechProviderLibraryService.testTts(
        providerId,
        text.trim(),
        voiceId.trim(),
        modelId && modelId.trim().length > 0 ? modelId.trim() : null,
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
      cleanupAudio();
      toast.error(await extractAudioApiError(err, "TTS leg failed."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card
      title="Text-to-speech (TTS leg)"
      icon={<Volume2 className="h-4 w-4" />}
    >
      <RecipeProviderHeader
        providerName={providerName}
        vendor={vendor}
        chips={[
          {
            label: "Voice",
            value: voiceId || "— missing —",
            tone: voiceId ? "default" : "destructive",
          },
          { label: "Model", value: modelId ?? "provider default" },
        ]}
      />
      <div className="space-y-2">
        <Label htmlFor="recipe-tts-test-text">Preview text</Label>
        <Textarea
          id="recipe-tts-test-text"
          rows={2}
          value={text}
          onChange={(e) => setText(e.target.value)}
          disabled={busy}
        />
      </div>
      <div className="flex items-center gap-2">
        {playing ? (
          <Button
            variant="destructive"
            onClick={() => cleanupAudio()}
            size="sm"
          >
            <Square className="h-4 w-4" />
            Stop
          </Button>
        ) : (
          <Button
            variant="secondary"
            onClick={() => void handlePlay()}
            disabled={busy || !voiceId}
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
    </Card>
  );
}

// ── STT card (chained) ───────────────────────────────────────────────────────

function ChainedSttCard({
  providerId,
  providerName,
  vendor,
  modelId,
  language,
}: {
  providerId: string;
  providerName: string;
  vendor: string;
  modelId: string | null;
  language: string | null;
}) {
  const recorder = usePcmRecorder({ sampleRate: STT_SAMPLE_RATE });
  const [transcribing, setTranscribing] = useState(false);
  const [transcript, setTranscript] = useState<string | null>(null);
  const [detectedLanguage, setDetectedLanguage] = useState<string | null>(null);

  const handleStart = async () => {
    setTranscript(null);
    setDetectedLanguage(null);
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
      setDetectedLanguage(result.language);
      toast.success("Transcription complete.");
    } catch (err) {
      toast.error(await extractAudioApiError(err, "STT leg failed."));
    } finally {
      setTranscribing(false);
    }
  };

  return (
    <Card title="Speech-to-text (STT leg)" icon={<Mic className="h-4 w-4" />}>
      <RecipeProviderHeader
        providerName={providerName}
        vendor={vendor}
        chips={[
          { label: "Model", value: modelId ?? "provider default" },
          { label: "Language", value: language ?? "auto" },
        ]}
      />
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
            {detectedLanguage && (
              <Badge variant="outline">{detectedLanguage}</Badge>
            )}
          </div>
          <p className="text-sm">{transcript}</p>
        </div>
      )}
    </Card>
  );
}

// ── Composite "coming soon" panel ────────────────────────────────────────────

function CompositeStatusCard({
  providerName,
  vendor,
  voice,
  model,
}: {
  providerName: string;
  vendor: string;
  voice: string;
  model: string | null;
}) {
  return (
    <Card title="Composite recipe (live)" icon={<Radio className="h-4 w-4" />}>
      <RecipeProviderHeader
        providerName={providerName}
        vendor={vendor}
        chips={[
          { label: "Voice", value: voice },
          { label: "Model", value: model ?? "provider default" },
        ]}
      />
      <div className="flex items-start gap-2 rounded-md border border-dashed bg-background p-3">
        <CircleAlert className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
        <div className="text-xs leading-relaxed text-muted-foreground">
          Composite recipes (OpenAI Realtime, Azure Voice Live) need a
          bidirectional WebSocket round-trip. The inline live test ships with
          the WSS pipeline phase (spec 024 phase G); for now, validate composite
          recipes by setting them as the active Voice Mode recipe and opening
          the live console from the mobile client.
        </div>
      </div>
    </Card>
  );
}

// ── Shared UI primitives ─────────────────────────────────────────────────────

function Card({
  title,
  icon,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-3 rounded-md border bg-muted/20 p-4">
      <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wider text-muted-foreground">
        <span className="grid h-5 w-5 place-items-center">{icon}</span>
        {title}
      </div>
      {children}
    </div>
  );
}

function RecipeProviderHeader({
  providerName,
  vendor,
  chips,
}: {
  providerName: string;
  vendor: string;
  chips: { label: string; value: string; tone?: "default" | "destructive" }[];
}) {
  return (
    <div className="space-y-1.5">
      <div className="text-sm font-semibold text-[var(--color-text-primary)]">
        {providerName}
      </div>
      <div className="text-[11px] text-[var(--color-text-secondary)]">
        {vendor}
      </div>
      <div className="flex flex-wrap gap-1.5 pt-1">
        {chips.map((c) => (
          <Badge
            key={c.label}
            variant={c.tone === "destructive" ? "error" : "outline"}
            className="font-mono text-[10.5px]"
          >
            {c.label}: {c.value}
          </Badge>
        ))}
      </div>
    </div>
  );
}

function MissingProviderCard({ label }: { label: string }) {
  return (
    <Card title={label} icon={<CircleAlert className="h-4 w-4" />}>
      <p className="text-xs text-destructive">
        The provider this recipe references is missing from your library — open
        the recipe editor and pick a different one.
      </p>
    </Card>
  );
}
