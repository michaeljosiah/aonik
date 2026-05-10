import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Plays a stream of 16-bit PCM chunks (typed `Int16Array` or raw `ArrayBuffer`) gaplessly via the
 * Web Audio API. Used by the live-pipeline test card to play back the bot's audio as it streams in
 * over the WebSocket.
 *
 * Each `enqueue(chunk)` schedules the chunk on a single timeline anchored at `nextPlayTime`,
 * so chunks land seamlessly even if they arrive in burst-then-quiet patterns. Sourced from the
 * playback scheduler in `samples/Voxa.Samples.AspNetServer/wwwroot/index.html`.
 */
export interface UsePcmStreamPlayerOptions {
  /** Sample rate of the incoming PCM (24 kHz for OpenAI/Azure/ElevenLabs/Mistral by default). */
  sampleRate?: number;
}

export interface UsePcmStreamPlayerResult {
  /** True once the AudioContext has been created and is running. */
  isReady: boolean;
  /** Last error from the underlying AudioContext / decode path. */
  error: string | null;
  /**
   * Schedule a PCM chunk for playback. Safe to call before the context resumes — chunks are
   * buffered until the user gesture resumes audio.
   */
  enqueue: (chunk: ArrayBuffer | Int16Array) => void;
  /** Reset the playback timeline (e.g. on interruption). Cancels any audio scheduled in the future. */
  reset: () => void;
  /** Tear down the AudioContext. Call from a cleanup effect. */
  dispose: () => Promise<void>;
}

export function usePcmStreamPlayer(
  options: UsePcmStreamPlayerOptions = {},
): UsePcmStreamPlayerResult {
  const { sampleRate = 24000 } = options;

  const audioContextRef = useRef<AudioContext | null>(null);
  const nextPlayTimeRef = useRef<number>(0);
  // Active source nodes — kept so reset() can stop scheduled-but-not-yet-played audio.
  const activeSourcesRef = useRef<Set<AudioBufferSourceNode>>(new Set());

  const [isReady, setIsReady] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const ensureContext = useCallback(() => {
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      return audioContextRef.current;
    }
    try {
      const ctx = new AudioContext({ sampleRate });
      audioContextRef.current = ctx;
      nextPlayTimeRef.current = ctx.currentTime;
      setIsReady(true);
      return ctx;
    } catch (err) {
      const message =
        err instanceof Error ? err.message : 'Failed to create audio context for playback.';
      setError(message);
      return null;
    }
  }, [sampleRate]);

  const enqueue = useCallback(
    (chunk: ArrayBuffer | Int16Array) => {
      const ctx = ensureContext();
      if (!ctx) return;

      // Auto-resume on first chunk arriving — modern browsers require a user gesture, but the
      // user clicking "Connect" / "Start" satisfies that, and the recorder's getUserMedia call
      // just before this implicitly resumed the page's audio policy.
      if (ctx.state === 'suspended') {
        void ctx.resume().catch((err: unknown) => {
          setError(err instanceof Error ? err.message : 'Failed to resume audio context.');
        });
      }

      const int16 =
        chunk instanceof Int16Array
          ? chunk
          : new Int16Array(chunk);
      if (int16.length === 0) return;

      // Convert 16-bit PCM → Float32 in [-1, 1].
      const float32 = new Float32Array(int16.length);
      for (let i = 0; i < int16.length; i++) {
        float32[i] = int16[i] / 0x8000;
      }

      const buffer = ctx.createBuffer(1, float32.length, ctx.sampleRate);
      buffer.copyToChannel(float32, 0);

      const source = ctx.createBufferSource();
      source.buffer = buffer;
      source.connect(ctx.destination);

      const startAt = Math.max(nextPlayTimeRef.current, ctx.currentTime);
      source.start(startAt);
      nextPlayTimeRef.current = startAt + buffer.duration;

      activeSourcesRef.current.add(source);
      source.onended = () => {
        activeSourcesRef.current.delete(source);
      };
    },
    [ensureContext],
  );

  const reset = useCallback(() => {
    const ctx = audioContextRef.current;
    if (!ctx) return;

    // Stop everything currently scheduled. Safe to call stop() on already-stopped sources; the
    // browser silently ignores it.
    for (const source of activeSourcesRef.current) {
      try {
        source.stop();
      } catch {
        /* already stopped */
      }
    }
    activeSourcesRef.current.clear();
    nextPlayTimeRef.current = ctx.currentTime;
  }, []);

  const dispose = useCallback(async () => {
    reset();
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      try {
        await audioContextRef.current.close();
      } catch {
        /* already closed */
      }
    }
    audioContextRef.current = null;
    setIsReady(false);
  }, [reset]);

  // Auto-cleanup on unmount.
  useEffect(() => {
    return () => {
      void dispose();
    };
  }, [dispose]);

  return { isReady, error, enqueue, reset, dispose };
}
