using System.Diagnostics;
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
    /// Per-chunk synth ceiling. Originally 5s, raised to 10s after a dev
    /// trace audit showed Mistral TTS regularly returning at 4.7–5.0s for
    /// normal-length sentences — i.e. right at the edge of the previous
    /// timeout. The wider window absorbs that variability without making
    /// the whole run feel stuck if one provider call truly hangs.
    /// </summary>
    public static readonly TimeSpan PerChunkTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Chunks that fail with a transient error (timeout, network fault,
    /// 5xx) get one quick retry on a fresh scope before we fall through
    /// to the speech.audio.error path. The retry is bounded by a tighter
    /// timeout so a genuinely stuck provider doesn't compound delays.
    /// </summary>
    public static readonly TimeSpan PerChunkRetryTimeout = TimeSpan.FromSeconds(7);

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
        var firstAttempt = await TrySynthAsync(
            messageId, chunkIndex, speechText, threadId,
            runCancellation, PerChunkTimeout, attempt: 1);

        if (firstAttempt.Succeeded)
        {
            Interlocked.Increment(ref _synthTasksCompleted);
            return;
        }

        // Run-level cancellation: client gone. Stop retrying.
        if (firstAttempt.Outcome == SynthAttemptOutcome.RunCancelled)
        {
            Interlocked.Increment(ref _synthTasksCancelled);
            return;
        }

        // If we already streamed bytes to the wire, retrying would
        // produce a discontinuous playback (duplicate header + missing
        // tail). Surface the partial chunk's failure instead of retrying.
        if (firstAttempt.AnyFramesEmitted)
        {
            await CountFailureAndEmitErrorAsync(
                messageId, chunkIndex, firstAttempt.Outcome,
                firstAttempt.ErrorMessage, threadId);
            return;
        }

        _logger.LogWarning(
            "voice TTS chunk first attempt failed ({Outcome}) — retrying once (chunkIndex={ChunkIndex}, threadId={ThreadId})",
            firstAttempt.Outcome, chunkIndex, threadId);

        var retryAttempt = await TrySynthAsync(
            messageId, chunkIndex, speechText, threadId,
            runCancellation, PerChunkRetryTimeout, attempt: 2);

        if (retryAttempt.Succeeded)
        {
            Interlocked.Increment(ref _synthTasksCompleted);
            return;
        }

        if (retryAttempt.Outcome == SynthAttemptOutcome.RunCancelled)
        {
            Interlocked.Increment(ref _synthTasksCancelled);
            return;
        }

        await CountFailureAndEmitErrorAsync(
            messageId, chunkIndex, retryAttempt.Outcome,
            retryAttempt.ErrorMessage, threadId);
    }

    private async Task<SynthAttemptResult> TrySynthAsync(
        string messageId,
        int chunkIndex,
        string speechText,
        string threadId,
        CancellationToken runCancellation,
        TimeSpan timeout,
        int attempt)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(runCancellation, _coordinatorCts.Token);
        linked.CancelAfter(timeout);
        var ct = linked.Token;

        var firstFrameSeen = false;

        // Per-chunk activity so the trace explorer shows one span per
        // synth attempt with its own duration, retry attempt number, and
        // any error tags. Without this, chunk-level failures were
        // invisible at the activity layer (only the dependency span for
        // the underlying HTTP call carried error metadata, which lacks
        // our per-chunk context like attempt number and chunkIndex).
        using var chunkActivity = AiTelemetry.ActivitySource.StartActivity(
            "aonik.chat.tts.chunk",
            ActivityKind.Internal);
        chunkActivity?.SetTag("aonik.chat.message_id", messageId);
        chunkActivity?.SetTag("aonik.chat.thread_id", threadId);
        chunkActivity?.SetTag("aonik.chat.chunk_index", chunkIndex);
        chunkActivity?.SetTag("aonik.chat.tts.attempt", attempt);
        chunkActivity?.SetTag("aonik.chat.tts.timeout_ms", (int)timeout.TotalMilliseconds);
        chunkActivity?.SetTag("aonik.chat.tts.text_length", speechText.Length);

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
                    if (!frame.Cached)
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

            chunkActivity?.SetTag("aonik.chat.tts.frames_emitted", firstFrameSeen);
            return SynthAttemptResult.Success(attempt, firstFrameSeen);
        }
        catch (OperationCanceledException timeoutEx) when (ct.IsCancellationRequested && !runCancellation.IsCancellationRequested)
        {
            AiTelemetry.MarkError(chunkActivity, timeoutEx);
            chunkActivity?.SetTag("aonik.chat.tts.outcome", "timeout");
            chunkActivity?.SetTag("aonik.chat.tts.frames_emitted", firstFrameSeen);
            return SynthAttemptResult.Failure(SynthAttemptOutcome.Timeout, $"TTS synthesis exceeded {timeout.TotalSeconds:0.#}s.", attempt, firstFrameSeen);
        }
        catch (OperationCanceledException cancelEx)
        {
            // Run-level cancellation: don't mark the chunk activity as
            // failed — the parent run was deliberately stopped, this
            // chunk just stopped along with it. We still annotate the
            // span so the trace shows why it ended early.
            chunkActivity?.SetTag("aonik.chat.tts.outcome", "run_cancelled");
            chunkActivity?.SetTag("aonik.chat.tts.frames_emitted", firstFrameSeen);
            _ = cancelEx;
            return SynthAttemptResult.Failure(SynthAttemptOutcome.RunCancelled, "Run cancelled.", attempt, firstFrameSeen);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(chunkActivity, ex);
            chunkActivity?.SetTag("aonik.chat.tts.outcome", "errored");
            chunkActivity?.SetTag("aonik.chat.tts.frames_emitted", firstFrameSeen);
            return SynthAttemptResult.Failure(SynthAttemptOutcome.Errored, ex.Message, attempt, firstFrameSeen);
        }
    }

    private async Task CountFailureAndEmitErrorAsync(
        string messageId,
        int chunkIndex,
        SynthAttemptOutcome outcome,
        string errorMessage,
        string threadId)
    {
        switch (outcome)
        {
            case SynthAttemptOutcome.Timeout:
                Interlocked.Increment(ref _synthTasksTimedOut);
                _logger.LogWarning(
                    "voice TTS chunk synthesis timed out after retry (chunkIndex={ChunkIndex}, threadId={ThreadId})",
                    chunkIndex, threadId);
                try
                {
                    await _writer.EmitAudioErrorAsync(messageId, chunkIndex, "timeout", errorMessage, CancellationToken.None);
                }
                catch
                {
                    // Connection already broken; nothing more to do.
                }
                break;
            default:
                Interlocked.Increment(ref _synthTasksErrored);
                _logger.LogWarning(
                    "voice TTS chunk synthesis failed after retry (chunkIndex={ChunkIndex}, threadId={ThreadId}, message={ErrorMessage})",
                    chunkIndex, threadId, errorMessage);
                try
                {
                    await _writer.EmitAudioErrorAsync(messageId, chunkIndex, "synth_failed", errorMessage, CancellationToken.None);
                }
                catch
                {
                    // Connection already broken.
                }
                break;
        }
    }

    private enum SynthAttemptOutcome
    {
        Succeeded,
        Timeout,
        Errored,
        RunCancelled,
    }

    private readonly record struct SynthAttemptResult(
        SynthAttemptOutcome Outcome,
        string ErrorMessage,
        int Attempt,
        bool AnyFramesEmitted)
    {
        public bool Succeeded => Outcome == SynthAttemptOutcome.Succeeded;

        public static SynthAttemptResult Success(int attempt, bool anyFramesEmitted)
            => new(SynthAttemptOutcome.Succeeded, string.Empty, attempt, anyFramesEmitted);

        public static SynthAttemptResult Failure(SynthAttemptOutcome outcome, string error, int attempt, bool anyFramesEmitted)
            => new(outcome, error, attempt, anyFramesEmitted);
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
