using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

/// <summary>
/// Fires per-chunk TTS synthesis tasks for voice-mode AGUI runs and pipes
/// the resulting audio frames into the prioritised
/// <see cref="AguiResponseWriter"/>. Bounded by a per-chunk timeout
/// (<see cref="PerChunkTimeout"/>); on timeout or provider error the
/// coordinator emits a <c>speech.audio.error</c> control event for the
/// affected chunk so the client can advance playback past it.
/// </summary>
/// <remarks>
/// <para>
/// Synthesis runs concurrent with the LLM stream — the LLM can emit
/// later text deltas while earlier sentences are still being synthesised.
/// At end-of-run the AGUI endpoint awaits
/// <see cref="WaitForAllSynthesisAsync"/> (so every chunk's frames have
/// been enqueued) before closing the audio channel and waiting on the
/// writer's drain.
/// </para>
/// </remarks>
internal sealed class VoiceSynthCoordinator : IAsyncDisposable
{
    /// <summary>
    /// Per-chunk synth ceiling. Confirmed default during sign-off — long
    /// enough to tolerate a slow ElevenLabs handshake, short enough that
    /// a flaky provider doesn't make the whole run feel stuck.
    /// </summary>
    public static readonly TimeSpan PerChunkTimeout = TimeSpan.FromSeconds(5);

    private readonly IStreamingTextToSpeechService _streamingTts;
    private readonly AguiResponseWriter _writer;
    private readonly string _providerFormat;
    private readonly string _mime;
    private readonly ILogger _logger;
    private readonly List<Task> _synthTasks = new();
    private readonly Lock _synthTasksLock = new();
    private readonly CancellationTokenSource _coordinatorCts = new();

    // Per-run counters for diagnosing audio-path drops. Read by the AGUI
    // endpoint at end-of-run to tag the chat activity, so traces can show
    // whether a missing audio chunk failed at synth start, mid-stream, or
    // never reached the wire.
    private int _synthTasksStarted;
    private int _synthTasksCompleted;
    private int _synthTasksErrored;
    private int _synthTasksTimedOut;
    private int _synthTasksCancelled;
    private int _synthTasksThatYieldedAtLeastOneFrame;

    public VoiceSynthCoordinator(
        IStreamingTextToSpeechService streamingTts,
        AguiResponseWriter writer,
        string providerFormat,
        string mime,
        ILogger logger)
    {
        _streamingTts = streamingTts;
        _writer = writer;
        _providerFormat = providerFormat;
        _mime = mime;
        _logger = logger;
    }

    /// <summary>
    /// Kick off synthesis for one speech chunk. Fire-and-forget — the
    /// returned task is tracked internally so
    /// <see cref="WaitForAllSynthesisAsync"/> can join on it. Errors are
    /// reported as <c>speech.audio.error</c> via <paramref name="writer"/>
    /// rather than thrown.
    /// </summary>
    public void StartChunkSynthesis(
        string messageId,
        int chunkIndex,
        string speechText,
        string threadId,
        CancellationToken runCancellation)
    {
        Interlocked.Increment(ref _synthTasksStarted);
        var task = Task.Run(() => RunChunkAsync(messageId, chunkIndex, speechText, threadId, runCancellation));
        lock (_synthTasksLock)
        {
            _synthTasks.Add(task);
        }
    }

    /// <summary>
    /// Snapshot of per-run synth task counters. Surfaced on the chat
    /// activity at end-of-run so traces can reveal whether a chunk was
    /// cancelled, errored, timed out, or simply never produced frames.
    /// </summary>
    public SynthTaskMetricsSnapshot GetSynthTaskMetrics() => new(
        Started: Interlocked.CompareExchange(ref _synthTasksStarted, 0, 0),
        Completed: Interlocked.CompareExchange(ref _synthTasksCompleted, 0, 0),
        Errored: Interlocked.CompareExchange(ref _synthTasksErrored, 0, 0),
        TimedOut: Interlocked.CompareExchange(ref _synthTasksTimedOut, 0, 0),
        Cancelled: Interlocked.CompareExchange(ref _synthTasksCancelled, 0, 0),
        YieldedAtLeastOneFrame: Interlocked.CompareExchange(ref _synthTasksThatYieldedAtLeastOneFrame, 0, 0));

    public async Task WaitForAllSynthesisAsync()
    {
        Task[] snapshot;
        lock (_synthTasksLock)
        {
            snapshot = _synthTasks.ToArray();
        }
        if (snapshot.Length == 0) return;

        try
        {
            await Task.WhenAll(snapshot);
        }
        catch
        {
            // Per-chunk failures are already surfaced as speech.audio.error
            // events; we swallow at this level so a single failed chunk
            // doesn't fail the whole run.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _coordinatorCts.Cancel();
        await WaitForAllSynthesisAsync();
        _coordinatorCts.Dispose();
    }

    private async Task RunChunkAsync(
        string messageId,
        int chunkIndex,
        string speechText,
        string threadId,
        CancellationToken runCancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(runCancellation, _coordinatorCts.Token);
        linked.CancelAfter(PerChunkTimeout);
        var ct = linked.Token;

        var firstFrameSeen = false;

        try
        {
            var request = new TextToSpeechSynthesisRequest(
                SpeechText: speechText,
                Locale: null,
                ThreadId: threadId,
                MessageId: messageId,
                UseCase: "payabo.chat.tts.stream",
                VoiceProfileOverride: new TextToSpeechVoiceProfile(
                    Provider: string.Empty,
                    VoiceId: string.Empty,
                    ModelId: null,
                    Locale: null,
                    OutputFormat: _providerFormat,
                    ProviderOptions: new Dictionary<string, string?>()));

            await foreach (var frame in _streamingTts.StreamSynthesizeAsync(request, ct).ConfigureAwait(false))
            {
                if (!firstFrameSeen)
                {
                    Interlocked.Increment(ref _synthTasksThatYieldedAtLeastOneFrame);
                    if (frame.Cached)
                    {
                        // Cache hits are recorded by the writer's pump as
                        // each frame flushes; nothing to do here.
                    }
                    else
                    {
                        _writer.RecordCacheMiss();
                    }
                    firstFrameSeen = true;
                }

                await _writer.EnqueueAudioFrameAsync(
                    messageId: messageId,
                    chunkIndex: chunkIndex,
                    data: frame.Data,
                    mime: _mime,
                    isFinal: frame.IsFinal,
                    cached: frame.Cached,
                    provider: frame.Provider,
                    voiceId: frame.VoiceId,
                    ttsAiRunId: frame.TtsAiRunId,
                    cancellationToken: ct).ConfigureAwait(false);
            }

            Interlocked.Increment(ref _synthTasksCompleted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !runCancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref _synthTasksTimedOut);
            _logger.LogWarning(
                "voice TTS chunk synthesis timed out (chunkIndex={ChunkIndex}, threadId={ThreadId})",
                chunkIndex, threadId);
            try
            {
                await _writer.EmitAudioErrorAsync(messageId, chunkIndex, "timeout", $"TTS synthesis exceeded {PerChunkTimeout.TotalSeconds:0.#}s.", CancellationToken.None);
            }
            catch
            {
                // Connection already broken; nothing more to do.
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _synthTasksCancelled);
            // Run-level cancellation — the client is gone. No error event.
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _synthTasksErrored);
            _logger.LogWarning(ex,
                "voice TTS chunk synthesis failed (chunkIndex={ChunkIndex}, threadId={ThreadId})",
                chunkIndex, threadId);
            try
            {
                await _writer.EmitAudioErrorAsync(messageId, chunkIndex, "synth_failed", ex.Message, CancellationToken.None);
            }
            catch
            {
                // Connection already broken.
            }
        }
    }
}

/// <summary>
/// Per-run snapshot of <see cref="VoiceSynthCoordinator"/> task counters.
/// </summary>
internal readonly record struct SynthTaskMetricsSnapshot(
    int Started,
    int Completed,
    int Errored,
    int TimedOut,
    int Cancelled,
    int YieldedAtLeastOneFrame)
{
    public int FailedToYieldAnyFrame => Started - YieldedAtLeastOneFrame;
}
