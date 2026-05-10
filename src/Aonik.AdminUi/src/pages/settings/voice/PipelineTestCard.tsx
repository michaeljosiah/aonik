import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, Loader2, Mic, Plug, Square } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/auth/useAuth';
import { usePcmRecorder } from '@/lib/audio/usePcmRecorder';
import { usePcmStreamPlayer } from '@/lib/audio/usePcmStreamPlayer';

type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'closing';

interface ConversationEntry {
  id: string;
  role: 'user' | 'assistant' | 'status' | 'system';
  text: string;
  timestamp: number;
}

const MIC_SAMPLE_RATE = 16000;       // sent to the server (Whisper-friendly)
const PLAYBACK_SAMPLE_RATE = 24000;  // bot audio comes back at 24 kHz

/**
 * End-to-end pipeline tester. Opens a WebSocket to <c>WSS /ai/voice</c>, sends a hello envelope,
 * streams mic PCM up, and renders the bot's transcription / text / audio / status / threadReady
 * envelopes as they arrive. This is the only in-admin way to exercise the full Phase B refactor
 * (STT → MAF agent → TTS round-trip).
 *
 * Exposes the wire envelopes directly in the conversation panel so issues are easy to diagnose:
 * a missing `transcription` event means STT, a missing first `text` event means the agent, and a
 * missing `speaking`/binary audio means TTS.
 */
export function PipelineTestCard() {
  const { getAccessToken } = useAuth();

  const [agentId, setAgentId] = useState<string>('personal-finance-agent');
  const [chatThreadId, setChatThreadId] = useState<string>('');
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
  const [conversation, setConversation] = useState<ConversationEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [botSpeaking, setBotSpeaking] = useState(false);
  const [threadReady, setThreadReady] = useState<{ id: string; isNew: boolean } | null>(null);

  const wsRef = useRef<WebSocket | null>(null);
  const conversationIdRef = useRef(0);

  const player = usePcmStreamPlayer({ sampleRate: PLAYBACK_SAMPLE_RATE });

  const appendEntry = useCallback((role: ConversationEntry['role'], text: string) => {
    if (!text.trim()) return;
    conversationIdRef.current += 1;
    setConversation((prev) => [
      ...prev,
      { id: `${conversationIdRef.current}`, role, text, timestamp: Date.now() },
    ]);
  }, []);

  const recorder = usePcmRecorder({
    sampleRate: MIC_SAMPLE_RATE,
    onChunk: (pcm) => {
      const ws = wsRef.current;
      if (ws && ws.readyState === WebSocket.OPEN) {
        // Send the underlying ArrayBuffer; matches WebSocketAudioSource's binary frame contract.
        ws.send(pcm.buffer);
      }
    },
  });

  // Compute the WSS URL once per component mount.
  const wsUrl = useMemo(() => {
    if (typeof window === 'undefined') return '';
    // The admin UI runs at :5173 in dev (Vite) but the API is on :5001. Use the API host.
    // In production both are typically same-origin via reverse proxy.
    const apiBase =
      (import.meta as unknown as { env?: { VITE_API_BASE_URL?: string } }).env?.VITE_API_BASE_URL ??
      window.location.origin;
    const url = new URL('/ai/voice', apiBase);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    return url.toString();
  }, []);

  const teardown = useCallback(async () => {
    setConnectionState('closing');
    try {
      await recorder.stop();
    } catch {
      /* ignore */
    }
    const ws = wsRef.current;
    wsRef.current = null;
    if (ws && ws.readyState === WebSocket.OPEN) {
      try {
        ws.send(JSON.stringify({ type: 'end' }));
        ws.close(1000, 'admin-disconnect');
      } catch {
        /* connection already gone */
      }
    }
    player.reset();
    setBotSpeaking(false);
    setConnectionState('disconnected');
  }, [player, recorder]);

  useEffect(() => {
    return () => {
      void teardown();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleConnect = async () => {
    if (!agentId.trim()) {
      toast.error('Enter an agent id (e.g. "personal-finance-agent").');
      return;
    }

    setError(null);
    setConversation([]);
    setThreadReady(null);
    setConnectionState('connecting');

    let token: string | null;
    try {
      token = await getAccessToken();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch auth token.';
      setError(message);
      setConnectionState('disconnected');
      return;
    }
    if (!token) {
      setError('No access token available. Sign in again.');
      setConnectionState('disconnected');
      return;
    }

    const url = `${wsUrl}?access_token=${encodeURIComponent(token)}`;
    let ws: WebSocket;
    try {
      ws = new WebSocket(url);
      ws.binaryType = 'arraybuffer';
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to open WebSocket.';
      setError(message);
      setConnectionState('disconnected');
      return;
    }
    wsRef.current = ws;

    ws.onopen = async () => {
      // Send hello envelope first thing.
      const hello = {
        type: 'hello',
        agentId: agentId.trim(),
        chatThreadId: chatThreadId.trim() || undefined,
        frontendTools: [] as string[],
        client: { app: 'admin-ui-pipeline-test' },
      };
      ws.send(JSON.stringify(hello));
      setConnectionState('connected');
      appendEntry('system', `Connected as ${agentId.trim()}.`);

      // Start mic streaming.
      try {
        await recorder.start();
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Microphone permission denied.';
        setError(message);
        await teardown();
      }
    };

    ws.onerror = () => {
      // The browser doesn't expose the actual error reason — fall back to generic message.
      setError('WebSocket error. Check the API is reachable and the auth policy allows admins.');
    };

    ws.onclose = (event) => {
      appendEntry('system', `Disconnected (${event.code}${event.reason ? `: ${event.reason}` : ''}).`);
      void teardown();
    };

    ws.onmessage = (event) => {
      if (event.data instanceof ArrayBuffer) {
        // Bot audio — schedule for gapless playback.
        player.enqueue(event.data);
        return;
      }

      // Text envelope — one of the WireProtocol shapes.
      try {
        const envelope = JSON.parse(event.data as string) as Record<string, unknown>;
        switch (envelope.type) {
          case 'transcription': {
            const text = String(envelope.text ?? '');
            const isFinal = Boolean(envelope.isFinal);
            if (isFinal && text.trim()) {
              appendEntry('user', text);
            }
            break;
          }
          case 'text': {
            const text = String(envelope.text ?? '');
            // Bot text — sentence-aggregated by the server.
            appendEntry('assistant', text);
            break;
          }
          case 'speaking': {
            if (envelope.who === 'bot') {
              setBotSpeaking(Boolean(envelope.started));
            }
            break;
          }
          case 'interruption': {
            player.reset();
            appendEntry('status', '⚡ Interruption — playback flushed.');
            break;
          }
          case 'status': {
            appendEntry('status', `🔧 ${String(envelope.message ?? '')}`);
            break;
          }
          case 'threadReady': {
            setThreadReady({
              id: String(envelope.chatThreadId ?? ''),
              isNew: Boolean(envelope.isNew),
            });
            break;
          }
          case 'toolCall': {
            // Frontend tool — we'd normally render UI for this. For testing, just log it and
            // immediately return an empty result so the agent can continue.
            const callId = String(envelope.callId ?? '');
            const name = String(envelope.name ?? '');
            appendEntry('status', `🛠 frontend tool call: ${name} (${callId}) — auto-acked`);
            ws.send(
              JSON.stringify({
                type: 'toolResult',
                callId,
                resultJson: '{"acknowledged":true}',
              }),
            );
            break;
          }
          case 'error': {
            const message = String(envelope.message ?? 'Server error.');
            setError(message);
            appendEntry('system', `❌ ${message}`);
            break;
          }
          case 'end': {
            appendEntry('system', 'Server signalled end.');
            break;
          }
          default:
            // Unknown envelope — surface for diagnostics.
            appendEntry('status', `(unknown envelope) ${event.data as string}`);
        }
      } catch {
        appendEntry('status', `(non-JSON text) ${event.data as string}`);
      }
    };
  };

  const handleDisconnect = async () => {
    await teardown();
  };

  // Two presentation states for the primary action button: showing "Connect" vs showing
  // "Disconnect". The Disconnect side covers both 'connected' and 'closing' so the user has a
  // visible spinner during the brief teardown window instead of the button flipping back to
  // "Connect" mid-animation.
  const showDisconnectButton: boolean =
    connectionState === 'connected' || connectionState === 'closing';
  const isBusy = connectionState === 'connecting' || connectionState === 'closing';

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Live pipeline test</CardTitle>
        <CardDescription>
          Open a WebSocket to <code className="font-mono text-xs">WSS /ai/voice</code>, stream your
          mic, and see the full pipeline run end-to-end (STT → agent → TTS). Frontend tool calls
          are auto-acknowledged with an empty result so the agent loop completes without any
          mobile-only UI.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="pipeline-agent">Agent id</Label>
            <Input
              id="pipeline-agent"
              value={agentId}
              onChange={(e) => setAgentId(e.target.value)}
              disabled={showDisconnectButton || isBusy}
              placeholder="personal-finance-agent"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="pipeline-thread">
              Resume thread id <span className="text-xs text-muted-foreground">(optional)</span>
            </Label>
            <Input
              id="pipeline-thread"
              value={chatThreadId}
              onChange={(e) => setChatThreadId(e.target.value)}
              disabled={showDisconnectButton || isBusy}
              placeholder="(blank = start new thread)"
            />
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {!showDisconnectButton ? (
            <Button onClick={() => void handleConnect()} disabled={isBusy}>
              {connectionState === 'connecting' ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Plug className="h-4 w-4" />
              )}
              Connect &amp; start mic
            </Button>
          ) : (
            <Button onClick={() => void handleDisconnect()} variant="destructive" disabled={isBusy}>
              {connectionState === 'closing' ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Square className="h-4 w-4" />
              )}
              Disconnect
            </Button>
          )}
          {connectionState === 'connected' && recorder.isRecording && (
            <Badge variant="default" className="gap-1">
              <Mic className="h-3 w-3" /> Mic streaming
            </Badge>
          )}
          {botSpeaking && (
            <Badge variant="secondary" className="animate-pulse">
              Bot speaking
            </Badge>
          )}
          {threadReady && (
            <Badge variant="outline" className="font-mono text-[10px]">
              {threadReady.isNew ? 'new' : 'resumed'}: {threadReady.id.slice(0, 8)}…
            </Badge>
          )}
        </div>

        {error && (
          <div className="flex items-start gap-2 rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
            <AlertCircle className="mt-0.5 h-4 w-4" />
            <span>{error}</span>
          </div>
        )}

        <div className="space-y-1 rounded-md border bg-muted/30 p-3">
          <div className="text-xs font-medium text-muted-foreground">Conversation</div>
          {conversation.length === 0 ? (
            <p className="py-2 text-sm text-muted-foreground">
              Connect, start speaking, and the user transcript + bot text + status events will
              appear here.
            </p>
          ) : (
            <div className="max-h-72 space-y-2 overflow-y-auto py-2">
              {conversation.map((entry) => (
                <ConversationLine key={entry.id} entry={entry} />
              ))}
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function ConversationLine({ entry }: { entry: ConversationEntry }) {
  const palette: Record<ConversationEntry['role'], string> = {
    user: 'border-l-blue-400 bg-blue-50 dark:bg-blue-950/20',
    assistant: 'border-l-emerald-400 bg-emerald-50 dark:bg-emerald-950/20',
    status: 'border-l-amber-400 bg-amber-50/40 dark:bg-amber-950/10 text-xs',
    system: 'border-l-slate-300 bg-slate-50/60 dark:bg-slate-950/10 text-xs',
  };
  const label: Record<ConversationEntry['role'], string> = {
    user: 'user',
    assistant: 'bot',
    status: 'status',
    system: 'system',
  };
  return (
    <div className={`rounded-sm border-l-2 px-2 py-1 text-sm ${palette[entry.role]}`}>
      <div className="text-[10px] uppercase tracking-wider text-muted-foreground">
        {label[entry.role]}
      </div>
      <div className="whitespace-pre-wrap">{entry.text}</div>
    </div>
  );
}
