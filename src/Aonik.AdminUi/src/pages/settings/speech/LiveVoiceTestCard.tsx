import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Loader2, Mic, MicOff, Plug, PlugZap, Square } from "lucide-react";
import { toast } from "sonner";

import { apiConfig } from "@/auth/authConfig";
import { useAuth } from "@/auth/useAuth";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { usePcmRecorder } from "@/lib/audio/usePcmRecorder";
import { usePcmStreamPlayer } from "@/lib/audio/usePcmStreamPlayer";
import { cn } from "@/lib/utils";

/**
 * Live voice-mode test card (spec 024 Phase E* / G follow-up). Replaces the disabled
 * <c>LiveTestCard</c> placeholder on the Voice Mode tab with a real WSS round-trip:
 * opens <c>/ai/voice</c>, sends a hello, streams mic PCM, plays the agent's PCM reply,
 * and renders a transcript trail.
 *
 * <para>
 * Wire format mirrors mobile (Voxa <c>WireProtocol</c>): binary frames carry 16-bit signed
 * LE PCM, text frames carry typed JSON envelopes. Auth is via <c>?access_token=</c>
 * because browser WebSocket can't add an Authorization header — the AONIK auth setup
 * already honours that fallback for this path (see <c>AonikAuthenticationSetup</c>).
 * </para>
 *
 * <para>
 * Admin role allowance: <c>MobileVoicePolicy</c> was widened to include PlatformAdmin /
 * TenantAdmin so admins can drive this card without faking a Payabo user account.
 * </para>
 */

type ConnectionStatus = "idle" | "connecting" | "connected" | "error";

interface TranscriptEntry {
  id: number;
  who: "user" | "bot" | "system";
  text: string;
  isFinal?: boolean;
}

interface LiveVoiceTestCardProps {
  /** Disables the start button when Voice Mode is off (matches the rest of the tab). */
  voiceModeEnabled: boolean;
  /** Active recipe display name — shown in the helper line so admins know what they're testing. */
  activeRecipeName: string | null;
  /** True if no recipe is selected — start button stays disabled. */
  hasActiveRecipe: boolean;
}

const DEFAULT_AGENT_ID = "personal-finance-agent";
// Recorder rate matches the chained-OpenAI pipeline contract (16 kHz, 16-bit PCM, mono).
// Output rate matches the OpenAI / Azure / ElevenLabs / Mistral default sink rate (24 kHz).
const RECORDER_SAMPLE_RATE = 16000;
const PLAYER_SAMPLE_RATE = 24000;

export function LiveVoiceTestCard({
  voiceModeEnabled,
  activeRecipeName,
  hasActiveRecipe,
}: LiveVoiceTestCardProps) {
  const { getAccessToken } = useAuth();

  const [agentId, setAgentId] = useState<string>(DEFAULT_AGENT_ID);
  const [status, setStatus] = useState<ConnectionStatus>("idle");
  const [transcript, setTranscript] = useState<TranscriptEntry[]>([]);
  const [whoIsSpeaking, setWhoIsSpeaking] = useState<"user" | "bot" | null>(
    null,
  );
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const wsRef = useRef<WebSocket | null>(null);
  const transcriptIdRef = useRef(0);

  const player = usePcmStreamPlayer({ sampleRate: PLAYER_SAMPLE_RATE });

  // Push every PCM chunk straight onto the WS. The recorder also internally buffers (so
  // stopAndCollect would still work), but we don't use that — streaming is fire-and-forget.
  const recorder = usePcmRecorder({
    sampleRate: RECORDER_SAMPLE_RATE,
    onChunk: (pcm) => {
      const ws = wsRef.current;
      if (ws && ws.readyState === WebSocket.OPEN) {
        // Send the underlying ArrayBuffer (Int16Array view's .buffer). Cast satisfies the
        // browser WebSocket overload that takes ArrayBuffer.
        ws.send(pcm.buffer as ArrayBuffer);
      }
    },
  });

  const appendTranscript = useCallback((entry: Omit<TranscriptEntry, "id">) => {
    setTranscript((prev) => {
      // If the most recent entry is a non-final transcription from the same speaker, REPLACE
      // it with the latest partial — Whisper streams partials before finals and we don't want
      // a wall of half-sentences.
      const last = prev[prev.length - 1];
      if (
        last &&
        last.who === entry.who &&
        last.isFinal === false &&
        entry.isFinal === false
      ) {
        return [...prev.slice(0, -1), { ...entry, id: last.id }];
      }
      transcriptIdRef.current += 1;
      return [...prev, { ...entry, id: transcriptIdRef.current }];
    });
  }, []);

  const cleanupConnection = useCallback(async () => {
    const ws = wsRef.current;
    wsRef.current = null;
    if (
      ws &&
      (ws.readyState === WebSocket.OPEN ||
        ws.readyState === WebSocket.CONNECTING)
    ) {
      try {
        ws.close();
      } catch {
        // already closed
      }
    }
    if (recorder.isRecording) {
      await recorder.stop();
    }
    player.reset();
    setWhoIsSpeaking(null);
  }, [player, recorder]);

  const handleTextEnvelope = useCallback(
    (raw: string) => {
      let envelope: { type?: string; [key: string]: unknown };
      try {
        envelope = JSON.parse(raw);
      } catch {
        return;
      }
      switch (envelope.type) {
        case "transcription": {
          const text = (envelope.text as string | undefined) ?? "";
          const isFinal = (envelope.isFinal as boolean | undefined) ?? false;
          if (text.trim().length > 0) {
            appendTranscript({ who: "user", text, isFinal });
          }
          break;
        }
        case "text": {
          const text = (envelope.text as string | undefined) ?? "";
          if (text.trim().length > 0) {
            // Bot text — server may stream as multiple chunks; merge consecutive bot entries
            // into one growing line. We do that inline here rather than in appendTranscript
            // because the rule is bot-text-specific.
            setTranscript((prev) => {
              const last = prev[prev.length - 1];
              if (last && last.who === "bot") {
                return [
                  ...prev.slice(0, -1),
                  { ...last, text: last.text + text, isFinal: true },
                ];
              }
              transcriptIdRef.current += 1;
              return [
                ...prev,
                {
                  id: transcriptIdRef.current,
                  who: "bot",
                  text,
                  isFinal: true,
                },
              ];
            });
          }
          break;
        }
        case "speaking": {
          const who = envelope.who as "user" | "bot" | undefined;
          const started = envelope.started as boolean | undefined;
          if (who && typeof started === "boolean") {
            setWhoIsSpeaking(started ? who : null);
          }
          break;
        }
        case "interruption": {
          // User barged in — flush any audio queued for playback so the bot stops mid-word.
          player.reset();
          break;
        }
        case "status": {
          const message = envelope.message as string | undefined;
          if (message) {
            appendTranscript({ who: "system", text: message });
          }
          break;
        }
        case "error": {
          const message =
            (envelope.message as string | undefined) ?? "Server error";
          setErrorMessage(message);
          break;
        }
        case "end": {
          appendTranscript({ who: "system", text: "Session ended by server." });
          break;
        }
        case "threadReady": {
          // AONIK extension — chat thread id assigned. Surface as a system note so admins can
          // confirm thread persistence is wired up.
          const threadId = envelope.chatThreadId as string | undefined;
          if (threadId) {
            appendTranscript({
              who: "system",
              text: `Thread ready: ${threadId.slice(0, 8)}…`,
            });
          }
          break;
        }
        default:
          // Unknown envelope — ignore. Voxa documents this as the right thing to do.
          break;
      }
    },
    [appendTranscript, player],
  );

  const handleStart = useCallback(async () => {
    if (!hasActiveRecipe) {
      toast.error("Pick an active Voice Mode recipe first.");
      return;
    }
    if (!agentId.trim()) {
      toast.error("Agent id is required.");
      return;
    }
    setErrorMessage(null);
    setTranscript([]);
    setStatus("connecting");

    let token: string | null;
    try {
      token = await getAccessToken();
    } catch (err) {
      setStatus("error");
      setErrorMessage(
        err instanceof Error ? err.message : "Failed to get access token.",
      );
      return;
    }
    if (!token) {
      setStatus("error");
      setErrorMessage("No access token — sign in again.");
      return;
    }

    const wsUrl = buildVoiceWsUrl(token);
    let ws: WebSocket;
    try {
      ws = new WebSocket(wsUrl);
    } catch (err) {
      setStatus("error");
      setErrorMessage(
        err instanceof Error ? err.message : "Failed to open WebSocket.",
      );
      return;
    }
    ws.binaryType = "arraybuffer";
    wsRef.current = ws;

    ws.onopen = () => {
      try {
        ws.send(
          JSON.stringify({
            type: "hello",
            agentId: agentId.trim(),
            frontendTools: [],
            client: { source: "admin-live-test" },
          }),
        );
      } catch (err) {
        setErrorMessage(
          err instanceof Error ? err.message : "Failed to send hello.",
        );
        return;
      }
      // Start the mic AFTER hello so we don't lose the first chunk to the connecting socket.
      void recorder
        .start()
        .then(() => setStatus("connected"))
        .catch((err: unknown) => {
          setStatus("error");
          setErrorMessage(
            err instanceof Error ? err.message : "Could not start microphone.",
          );
          try {
            ws.close();
          } catch {
            /* ignore */
          }
        });
    };

    ws.onmessage = (event: MessageEvent) => {
      if (typeof event.data === "string") {
        handleTextEnvelope(event.data);
      } else if (event.data instanceof ArrayBuffer) {
        // Server-emitted PCM. Voxa's WebSocketAudioSink writes raw PCM as binary frames.
        player.enqueue(event.data);
      }
    };

    ws.onerror = () => {
      // The browser surfaces almost no detail in onerror. The follow-on onclose has a code.
      setStatus("error");
      setErrorMessage(
        (prev) => prev ?? "WebSocket error — see browser console for details.",
      );
    };

    ws.onclose = (event) => {
      void cleanupConnection();
      if (event.wasClean || event.code === 1000) {
        setStatus("idle");
      } else {
        setStatus("error");
        setErrorMessage(
          (prev) =>
            prev ??
            `Connection closed (code ${event.code}${event.reason ? `: ${event.reason}` : ""}).`,
        );
      }
    };
  }, [
    agentId,
    cleanupConnection,
    getAccessToken,
    handleTextEnvelope,
    hasActiveRecipe,
    player,
    recorder,
  ]);

  const handleStop = useCallback(async () => {
    const ws = wsRef.current;
    if (ws && ws.readyState === WebSocket.OPEN) {
      try {
        ws.send(JSON.stringify({ type: "end" }));
      } catch {
        // ignore
      }
    }
    await cleanupConnection();
    setStatus("idle");
  }, [cleanupConnection]);

  // Tear down on unmount so a tab switch mid-conversation doesn't leak the mic + WS.
  useEffect(() => {
    return () => {
      void cleanupConnection();
    };
    // cleanupConnection is stable (useCallback over stable deps); empty array is intentional.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const isLive = status === "connected" || status === "connecting";

  const helperText = useMemo(() => {
    if (!hasActiveRecipe) {
      return "Pick an active recipe above before running the live test.";
    }
    if (!voiceModeEnabled) {
      return `Voice Mode is off — the test still runs, but mobile users can't connect until it's turned on.`;
    }
    return activeRecipeName
      ? `Talk through the active recipe: ${activeRecipeName}.`
      : "Talk through the active recipe.";
  }, [activeRecipeName, hasActiveRecipe, voiceModeEnabled]);

  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <div className="flex items-center justify-between gap-2">
        <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">
          Live test
        </div>
        <ConnectionPill status={status} />
      </div>
      <p className="mt-1 mb-3 text-xs leading-relaxed text-[var(--color-text-secondary)]">
        {helperText}
      </p>

      <div className="space-y-2">
        <Label htmlFor="live-test-agent-id" className="text-[11px]">
          Agent
        </Label>
        <Input
          id="live-test-agent-id"
          value={agentId}
          onChange={(e) => setAgentId(e.target.value)}
          disabled={isLive}
          placeholder="agent id, e.g. personal-finance-agent"
          className="h-8 text-[12px]"
        />
      </div>

      <div className="mt-3 flex items-center gap-2">
        {status === "connected" ? (
          <Button
            variant="destructive"
            size="sm"
            className="w-full justify-center"
            onClick={() => void handleStop()}
          >
            <Square className="h-3.5 w-3.5" /> Stop
          </Button>
        ) : (
          <Button
            size="sm"
            className="w-full justify-center"
            onClick={() => void handleStart()}
            disabled={!hasActiveRecipe || status === "connecting"}
          >
            {status === "connecting" ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <Mic className="h-3.5 w-3.5" />
            )}
            {status === "connecting" ? "Connecting…" : "Start voice test"}
          </Button>
        )}
      </div>

      {errorMessage && (
        <div className="mt-3 rounded-md border border-[var(--color-error-50)] bg-[var(--color-error-10)] px-3 py-2 text-[11px] text-[var(--color-error)]">
          {errorMessage}
        </div>
      )}

      {/* Mic / speaker indicators. Mirror the disabled placeholder's look but populated from
          live state — the brand-color border lights up when the recorder is capturing, and the
          speaking indicator flips between "you" and "bot". */}
      <div className="mt-3 flex items-center gap-3 rounded-[10px] border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3.5">
        <div
          className={cn(
            "grid h-9 w-9 shrink-0 place-items-center rounded-full transition-colors",
            recorder.isRecording
              ? "bg-[var(--color-brand-primary-10)]"
              : "bg-[var(--color-surface)]",
          )}
        >
          {recorder.isRecording ? (
            <Mic className="h-4 w-4 text-[var(--color-brand-primary)]" />
          ) : (
            <MicOff className="h-4 w-4 text-[var(--color-text-tertiary)]" />
          )}
        </div>
        <div className="flex-1 text-[11px] text-[var(--color-text-secondary)]">
          {recorder.isRecording ? (
            <>
              Mic streaming · {RECORDER_SAMPLE_RATE / 1000} kHz PCM
              {whoIsSpeaking === "bot" && (
                <span className="ml-2 inline-flex items-center gap-1 text-[var(--color-brand-primary)]">
                  <PlugZap className="h-3 w-3" /> Bot speaking
                </span>
              )}
              {whoIsSpeaking === "user" && (
                <span className="ml-2 inline-flex items-center gap-1 text-[var(--color-brand-primary)]">
                  <Plug className="h-3 w-3" /> You're speaking
                </span>
              )}
            </>
          ) : (
            <>Mic idle</>
          )}
        </div>
      </div>

      {transcript.length > 0 && (
        <div className="mt-3 space-y-1.5">
          <div className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
            Transcript
          </div>
          <div className="max-h-48 space-y-1.5 overflow-y-auto rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-2.5">
            {transcript.map((entry) => (
              <div
                key={entry.id}
                className="flex items-start gap-2 text-[12px]"
              >
                <Badge
                  variant="outline"
                  className={cn(
                    "shrink-0 font-mono text-[10px]",
                    entry.who === "user" &&
                      "border-[var(--color-brand-primary)] text-[var(--color-brand-primary)]",
                    entry.who === "bot" &&
                      "border-[var(--color-success)] text-[var(--color-success)]",
                    entry.who === "system" &&
                      "text-[var(--color-text-tertiary)]",
                  )}
                >
                  {entry.who}
                </Badge>
                <span
                  className={cn(
                    "leading-snug",
                    entry.isFinal === false &&
                      "italic text-[var(--color-text-secondary)]",
                  )}
                >
                  {entry.text}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function ConnectionPill({ status }: { status: ConnectionStatus }) {
  switch (status) {
    case "connected":
      return (
        <Badge
          variant="outline"
          className="animate-pulse border-[var(--color-success)] text-[var(--color-success)]"
        >
          Live
        </Badge>
      );
    case "connecting":
      return <Badge variant="outline">Connecting</Badge>;
    case "error":
      return <Badge variant="error">Error</Badge>;
    default:
      return (
        <Badge variant="outline" className="text-[var(--color-text-tertiary)]">
          Idle
        </Badge>
      );
  }
}

/**
 * Build the WSS URL for the voice endpoint. The Aonik auth setup honours
 * <c>?access_token=</c> on this path because browser WebSocket can't add an Authorization
 * header (Flutter has the same constraint, which is why the fallback exists at all).
 *
 * <para>
 * <c>apiConfig.baseUrl</c> can be either:
 * <list type="bullet">
 *   <item><description>relative ("/api") — dev / production browser builds</description></item>
 *   <item><description>absolute (electron / VITE_API_BASE_URL override)</description></item>
 * </list>
 * </para>
 */
function buildVoiceWsUrl(token: string): string {
  const baseUrl = apiConfig.baseUrl;
  const search = `?access_token=${encodeURIComponent(token)}`;

  if (baseUrl.startsWith("http://") || baseUrl.startsWith("https://")) {
    const url = new URL(baseUrl);
    url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
    url.pathname = `${url.pathname.replace(/\/+$/, "")}/ai/voice`;
    url.search = search;
    return url.toString();
  }

  // Relative — use the page's origin with the ws/wss scheme. baseUrl is typically "/api".
  const proto = window.location.protocol === "https:" ? "wss:" : "ws:";
  const path = `${baseUrl.replace(/\/+$/, "")}/ai/voice`;
  return `${proto}//${window.location.host}${path}${search}`;
}
