namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Public boundary for streaming text-to-speech synthesis. Yields audio
/// frames as they arrive from the underlying provider, so callers (e.g.
/// the AGUI streaming endpoint when running in voice mode) can forward
/// audio downstream incrementally without waiting for the full synthesis
/// to complete.
/// </summary>
/// <remarks>
/// <para>
/// Implementations apply the same tenant settings, credentials,
/// rate-limiting, normalization, and AiRun audit lifecycle as the
/// non-streaming <see cref="ITextToSpeechService"/>. They are <em>not</em>
/// a thin wrapper over the provider — the public service is the policy
/// boundary.
/// </para>
/// <para>
/// On a cache hit the implementation yields a single
/// <see cref="TtsAudioFrame"/> with <see cref="TtsAudioFrame.Cached"/> set
/// to <c>true</c> and <see cref="TtsAudioFrame.IsFinal"/> set to
/// <c>true</c>. On cancellation the stream stops; any pending AiRun is
/// marked failed.
/// </para>
/// </remarks>
public interface IStreamingTextToSpeechService
{
    /// <summary>
    /// Synthesizes <paramref name="request"/> and yields audio frames in
    /// arrival order. The caller is responsible for ordering relative to
    /// other concurrent calls — the service does not interleave frames
    /// across requests.
    /// </summary>
    IAsyncEnumerable<TtsAudioFrame> StreamSynthesizeAsync(
        TextToSpeechSynthesisRequest request,
        CancellationToken cancellationToken = default);
}
