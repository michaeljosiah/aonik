using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Services;

/// <summary>
/// Wraps the AG-UI SSE response stream with prioritised write paths:
/// <list type="bullet">
///   <item>Control events (RUN_*, TEXT_MESSAGE_*, TOOL_CALL_*, REASONING_*,
///         speech.chunk / speech.render / speech.audio.error) write
///         directly under the response-stream mutex and never wait on
///         queued audio.</item>
///   <item>Audio events (speech.audio frames) flow through a bounded
///         channel that is drained by a background pump task. Channel
///         overflow drops with synchronous <c>speech.audio.error
///         code=backpressure_dropped</c> control events so the audio path
///         never stalls the run.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// In non-voice runs (<c>voiceMode == false</c>) the audio channel is
/// inert — every method short-circuits to a single locked write and
/// <see cref="WaitForAudioDrainAsync"/> returns a completed task. This
/// keeps the existing AG-UI behaviour byte-for-byte identical for
/// non-voice clients.
/// </para>
/// <para>
/// The drain contract honours the reviewer's refinement: callers wait
/// on <em>writer flushes</em>, not on synth-task completion. Producers
/// must call <see cref="CompleteAudioInputAsync"/> first to signal "no
/// more frames will be enqueued" before awaiting drain.
/// </para>
/// </remarks>
internal sealed class AguiResponseWriter : IAsyncDisposable
{
    // Bounded queue of audio frames waiting to be flushed to the wire.
    // Keep this small — the per-frame payload is base64 audio (~21 KB),
    // and we'd rather drop a frame and emit an error than buffer a long
    // backlog that delays text responses.
    private const int AudioChannelCapacity = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly HttpResponse _response;
    private readonly bool _voiceMode;
    private readonly Stopwatch _wallClock;
    private readonly SemaphoreSlim _writeLock = new(initialCount: 1, maxCount: 1);
    private readonly Channel<AudioFrameEnvelope>? _audioChannel;
    private readonly Task _audioPumpTask;
    private readonly CancellationTokenSource _audioCts = new();

    private long _audioBytesWritten;
    private long _audioFramesWritten;
    private long _audioFramesDropped;
    private long _audioCacheHits;
    private long _audioCacheMisses;
    private long _firstAudibleByteMs = -1;
    private long _audioDrainStartMs = -1;
    private long _audioDrainCompletedMs = -1;
    private int _seq;

    public AguiResponseWriter(HttpResponse response, bool voiceMode, Stopwatch wallClock)
    {
        _response = response;
        _voiceMode = voiceMode;
        _wallClock = wallClock;

        if (voiceMode)
        {
            // Wait mode (not DropWrite) so TryWrite returns false on a full
            // channel — we want to detect overflow explicitly and emit a
            // speech.audio.error control event, not silently drop frames.
            _audioChannel = Channel.CreateBounded<AudioFrameEnvelope>(new BoundedChannelOptions(AudioChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
            _audioPumpTask = Task.Run(PumpAudioAsync);
        }
        else
        {
            _audioChannel = null;
            _audioPumpTask = Task.CompletedTask;
        }
    }

    public AudioMetricsSnapshot GetAudioMetrics() => new(
        VoiceMode: _voiceMode,
        AudioBytes: Interlocked.Read(ref _audioBytesWritten),
        AudioFrames: Interlocked.Read(ref _audioFramesWritten),
        AudioFramesDropped: Interlocked.Read(ref _audioFramesDropped),
        TtsCacheHits: Interlocked.Read(ref _audioCacheHits),
        TtsCacheMisses: Interlocked.Read(ref _audioCacheMisses),
        FirstAudibleByteMs: Interlocked.Read(ref _firstAudibleByteMs) is var first && first >= 0 ? first : null,
        AudioDrainMs: ResolveDrainMs());

    public void RecordCacheHit() => Interlocked.Increment(ref _audioCacheHits);
    public void RecordCacheMiss() => Interlocked.Increment(ref _audioCacheMisses);

    /// <summary>
    /// Write a control event (text deltas, tool calls, RUN_*, speech.chunk,
    /// speech.render, speech.audio.error). Always preempts queued audio
    /// because audio frames must take the same lock and the lock is FIFO.
    /// </summary>
    public async Task WriteControlAsync<T>(T eventData, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(eventData, JsonOptions);
            await _response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await _response.Body.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Enqueue an audio frame for the background pump to flush. No-op when
    /// <c>voiceMode == false</c>. On overflow, increments the drop counter
    /// and writes a <c>speech.audio.error</c> control event (so the error
    /// path never blocks behind the saturated audio queue).
    /// </summary>
    public async Task EnqueueAudioFrameAsync(
        string messageId,
        int chunkIndex,
        ReadOnlyMemory<byte> data,
        string mime,
        bool isFinal,
        bool cached,
        string provider,
        string voiceId,
        Guid? ttsAiRunId,
        CancellationToken cancellationToken)
    {
        if (!_voiceMode || _audioChannel is null)
        {
            return;
        }

        var seq = Interlocked.Increment(ref _seq);
        var envelope = new AudioFrameEnvelope(
            MessageId: messageId,
            ChunkIndex: chunkIndex,
            Seq: seq,
            Data: data,
            Mime: mime,
            IsFinal: isFinal,
            Cached: cached,
            Provider: provider,
            VoiceId: voiceId,
            TtsAiRunId: ttsAiRunId);

        if (_audioChannel.Writer.TryWrite(envelope))
        {
            return;
        }

        // Overflow path — surface as a control-channel error so the client
        // can advance playback past this chunk without waiting on audio.
        Interlocked.Increment(ref _audioFramesDropped);
        await WriteControlAsync(new
        {
            type = "CUSTOM",
            name = "speech.audio.error",
            value = new
            {
                messageId,
                chunkIndex,
                code = "backpressure_dropped",
                message = "Audio frame dropped due to backpressure.",
                isFinal = true,
            },
        }, cancellationToken);
    }

    /// <summary>
    /// Synchronously emit a <c>speech.audio.error</c> control event for a
    /// chunk whose synthesis failed (timeout, provider error, etc.). The
    /// pump may still drain previously enqueued frames for the same
    /// chunk; the <c>isFinal=true</c> flag tells the client to advance
    /// playback ordering past this chunk regardless.
    /// </summary>
    public Task EmitAudioErrorAsync(string messageId, int chunkIndex, string code, string message, CancellationToken cancellationToken) =>
        WriteControlAsync(new
        {
            type = "CUSTOM",
            name = "speech.audio.error",
            value = new
            {
                messageId,
                chunkIndex,
                code,
                message,
                isFinal = true,
            },
        }, cancellationToken);

    /// <summary>
    /// Signal that no more audio frames will be enqueued. Producers MUST
    /// call this before awaiting <see cref="WaitForAudioDrainAsync"/> —
    /// the pump's drain loop only exits once the channel is completed AND
    /// empty.
    /// </summary>
    public void CompleteAudioInput()
    {
        _audioChannel?.Writer.TryComplete();
        if (_voiceMode)
        {
            Interlocked.CompareExchange(ref _audioDrainStartMs, _wallClock.ElapsedMilliseconds, -1);
        }
    }

    /// <summary>
    /// Awaits the pump task. Resolves when every enqueued audio frame has
    /// been written and flushed to the wire — <em>not</em> when synth
    /// workers finished. The reviewer's refinement.
    /// </summary>
    public async Task WaitForAudioDrainAsync()
    {
        if (!_voiceMode)
        {
            return;
        }

        try
        {
            await _audioPumpTask;
        }
        finally
        {
            Interlocked.CompareExchange(ref _audioDrainCompletedMs, _wallClock.ElapsedMilliseconds, -1);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _audioChannel?.Writer.TryComplete();
        _audioCts.Cancel();

        try
        {
            await _audioPumpTask;
        }
        catch
        {
            // Pump exits on cancellation; nothing else to do.
        }

        _writeLock.Dispose();
        _audioCts.Dispose();
    }

    private async Task PumpAudioAsync()
    {
        if (_audioChannel is null) return;
        await foreach (var envelope in _audioChannel.Reader.ReadAllAsync(_audioCts.Token).ConfigureAwait(false))
        {
            if (_audioCts.IsCancellationRequested) break;

            // Capture first-audible-byte before we record any framing
            // overhead so the metric reflects when the user could
            // actually start hearing audio.
            if (envelope.Data.Length > 0)
            {
                Interlocked.CompareExchange(ref _firstAudibleByteMs, _wallClock.ElapsedMilliseconds, -1);
            }

            if (envelope.Cached)
            {
                Interlocked.Increment(ref _audioCacheHits);
            }
            // Misses are recorded by the synth worker, not here, because
            // the same chunk can produce many frames; we only want to
            // count the chunk once. RecordCacheMiss is invoked on the
            // first non-cached frame of a chunk.

            try
            {
                await WriteControlAsync(new
                {
                    type = "CUSTOM",
                    name = "speech.audio",
                    value = new
                    {
                        messageId = envelope.MessageId,
                        chunkIndex = envelope.ChunkIndex,
                        seq = envelope.Seq,
                        mime = envelope.Mime,
                        encoding = "base64",
                        data = Convert.ToBase64String(envelope.Data.Span),
                        isFinal = envelope.IsFinal,
                        cached = envelope.Cached,
                        provider = envelope.Provider,
                        voiceId = envelope.VoiceId,
                        ttsAiRunId = envelope.TtsAiRunId,
                    },
                }, CancellationToken.None);

                Interlocked.Increment(ref _audioFramesWritten);
                Interlocked.Add(ref _audioBytesWritten, envelope.Data.Length);
            }
            catch (Exception)
            {
                // The response stream is broken — there's nowhere to
                // surface this. Stop pumping; the disposal path will
                // observe the broken connection.
                break;
            }
        }
    }

    private long? ResolveDrainMs()
    {
        var start = Interlocked.Read(ref _audioDrainStartMs);
        var done = Interlocked.Read(ref _audioDrainCompletedMs);
        if (start < 0 || done < 0) return null;
        return Math.Max(0, done - start);
    }

    private readonly record struct AudioFrameEnvelope(
        string MessageId,
        int ChunkIndex,
        int Seq,
        ReadOnlyMemory<byte> Data,
        string Mime,
        bool IsFinal,
        bool Cached,
        string Provider,
        string VoiceId,
        Guid? TtsAiRunId);
}

internal readonly record struct AudioMetricsSnapshot(
    bool VoiceMode,
    long AudioBytes,
    long AudioFrames,
    long AudioFramesDropped,
    long TtsCacheHits,
    long TtsCacheMisses,
    long? FirstAudibleByteMs,
    long? AudioDrainMs);
