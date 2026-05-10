import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * PCM mic recorder for the voice testing surface. Captures Float32 mic samples through an
 * `AudioWorkletNode` (the worklet lives at `/audio/voice-recorder-worklet.js`), converts each
 * chunk to 16-bit signed PCM, and either:
 *
 * - **Buffers** it into a single Int16Array for the "Test STT" record-then-upload flow, or
 * - **Streams** it via the `onChunk` callback for the live-pipeline mic-to-WebSocket flow.
 *
 * Mirrors the wire format Voxa's `WebSocketAudioSource` consumes (16-bit signed LE PCM, mono).
 */
export interface UsePcmRecorderOptions {
  /** Target capture rate. The browser resamples the mic stream to this. */
  sampleRate?: number;
  /** Frames per worklet message. 800 = 50 ms at 16 kHz / 33 ms at 24 kHz. */
  chunkSamples?: number;
  /**
   * Streaming sink. Called for every PCM chunk the worklet emits. Use this for the live-pipeline
   * card. Omit (and call `stopAndCollect()`) if you just want a single buffered clip.
   */
  onChunk?: (pcm: Int16Array) => void;
}

export interface UsePcmRecorderResult {
  isRecording: boolean;
  /** Permission / device-init error, surfaced to the UI. Cleared on each start. */
  error: string | null;
  /** Negotiated capture sample rate (set after the first start). */
  sampleRate: number | null;
  start: () => Promise<void>;
  /** Stop and discard buffered audio. Use when streaming via `onChunk`. */
  stop: () => Promise<void>;
  /** Stop and return everything captured since `start()` as a single Int16Array. */
  stopAndCollect: () => Promise<Int16Array | null>;
}

export function usePcmRecorder(options: UsePcmRecorderOptions = {}): UsePcmRecorderResult {
  const { sampleRate: requestedRate = 16000, chunkSamples = 800, onChunk } = options;

  const audioContextRef = useRef<AudioContext | null>(null);
  const sourceNodeRef = useRef<MediaStreamAudioSourceNode | null>(null);
  const workletNodeRef = useRef<AudioWorkletNode | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const collectedChunksRef = useRef<Int16Array[]>([]);
  const onChunkRef = useRef<typeof onChunk>(onChunk);

  const [isRecording, setIsRecording] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sampleRate, setSampleRate] = useState<number | null>(null);

  // Keep the latest onChunk reachable from the worklet message handler (which we wire once).
  useEffect(() => {
    onChunkRef.current = onChunk;
  }, [onChunk]);

  const cleanup = useCallback(async () => {
    try {
      workletNodeRef.current?.disconnect();
    } catch {
      /* already disconnected */
    }
    workletNodeRef.current = null;

    try {
      sourceNodeRef.current?.disconnect();
    } catch {
      /* already disconnected */
    }
    sourceNodeRef.current = null;

    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;

    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      try {
        await audioContextRef.current.close();
      } catch {
        /* already closed */
      }
    }
    audioContextRef.current = null;
  }, []);

  const start = useCallback(async () => {
    setError(null);
    collectedChunksRef.current = [];

    // Reuse-or-create. Each "session" is single-use to keep state simple.
    await cleanup();

    let stream: MediaStream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Microphone permission denied.';
      setError(message);
      throw err;
    }
    streamRef.current = stream;

    const audioContext = new AudioContext({ sampleRate: requestedRate });
    audioContextRef.current = audioContext;
    setSampleRate(audioContext.sampleRate);

    try {
      await audioContext.audioWorklet.addModule('/audio/voice-recorder-worklet.js');
    } catch (err) {
      const message =
        err instanceof Error
          ? `Failed to load mic worklet: ${err.message}`
          : 'Failed to load mic worklet.';
      setError(message);
      await cleanup();
      throw err;
    }

    const source = audioContext.createMediaStreamSource(stream);
    sourceNodeRef.current = source;

    const worklet = new AudioWorkletNode(audioContext, 'voice-recorder', {
      processorOptions: { chunkSamples },
    });
    workletNodeRef.current = worklet;

    worklet.port.onmessage = (event: MessageEvent<ArrayBuffer>) => {
      const pcm = new Int16Array(event.data);
      // Always buffer — caller may call stopAndCollect even when streaming via onChunk.
      collectedChunksRef.current.push(pcm);
      onChunkRef.current?.(pcm);
    };

    source.connect(worklet);
    // Don't connect to destination — we don't want monitoring playback.

    setIsRecording(true);
  }, [chunkSamples, cleanup, requestedRate]);

  const stop = useCallback(async () => {
    setIsRecording(false);
    await cleanup();
  }, [cleanup]);

  const stopAndCollect = useCallback(async (): Promise<Int16Array | null> => {
    setIsRecording(false);
    await cleanup();

    const chunks = collectedChunksRef.current;
    if (chunks.length === 0) return null;

    const totalLength = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
    const merged = new Int16Array(totalLength);
    let offset = 0;
    for (const chunk of chunks) {
      merged.set(chunk, offset);
      offset += chunk.length;
    }
    collectedChunksRef.current = [];
    return merged;
  }, [cleanup]);

  // Auto-cleanup on unmount.
  useEffect(() => {
    return () => {
      void cleanup();
    };
  }, [cleanup]);

  return { isRecording, error, sampleRate, start, stop, stopAndCollect };
}
