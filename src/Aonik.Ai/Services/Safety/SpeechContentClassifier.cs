using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Turns generated speech into text so it can be judged as text. A vendor seam, deliberately narrow.
///
/// <para>
/// Not registered by default, for the same reason no classification adapter is: no transcription
/// vendor is configured in this solution, and a stub that returned an empty transcript would look like
/// coverage while providing none — the worst of the available options.
/// </para>
/// </summary>
public interface ISpeechTranscriber
{
    /// <summary>
    /// Transcribe audio at <paramref name="reference"/>. Throwing is a legitimate outcome: the gate
    /// turns it into <c>CheckUnavailable</c> and the narration is not played.
    /// </summary>
    Task<SpeechTranscript> TranscribeAsync(
        Guid subjectPartyId,
        string reference,
        CancellationToken cancellationToken = default);
}

/// <param name="RunId">The <c>AiRun</c> for the transcription. Recorded on the decision like any other.</param>
public sealed record SpeechTranscript(string Text, Guid RunId);

/// <summary>
/// Spec 096 S5 — classification for generated narration, in two legs that both have to run.
///
/// <para>
/// <strong>A transcript is not the artefact.</strong> "And then the door opened" is unremarkable as
/// text and can be delivered in a voice that terrifies a six-year-old — content that is frightening in
/// <em>performance</em> rather than in words. So this classifier transcribes and classifies the text,
/// <em>and separately</em> classifies the audio for the delivery characteristics a transcript cannot
/// carry: tone, pacing, distress, whispering, screaming. Either leg missing means the modality is
/// unjudged, and unjudged content is not delivered.
/// </para>
///
/// <para>
/// <strong>Voice does not inherit coverage from another modality.</strong> Registering an image or
/// video adapter enables nothing here — the speech leg resolves its own route and needs an adapter
/// that names <c>speech</c> among its modalities. That is the phrasing Spec 096 S5 uses, and it is
/// worth enforcing structurally rather than remembering: a product that ships video classification and
/// quietly assumes narration is covered has an unclassified path to a child's ears.
/// </para>
/// </summary>
internal sealed class SpeechContentClassifier : IContentClassifier, ITemporalCoverage
{
    private readonly ISpeechTranscriber? _transcriber;
    private readonly IContentClassifier? _transcriptClassifier;
    private readonly IContentClassifier? _audioClassifier;
    private readonly ILogger<SpeechContentClassifier> _logger;

    public SpeechContentClassifier(
        ISpeechTranscriber? transcriber,
        IContentClassifier? transcriptClassifier,
        IContentClassifier? audioClassifier,
        ILogger<SpeechContentClassifier> logger)
    {
        _transcriber = transcriber;
        _transcriptClassifier = transcriptClassifier;
        _audioClassifier = audioClassifier;
        _logger = logger;
    }

    public string Modality => SafetyModalities.Speech;

    /// <summary>
    /// Both legs run over the whole artefact — the full transcript, and the full waveform. Speech has
    /// the same temporal hole video does, so the claim is declared rather than assumed: a future
    /// implementation that classified one-second windows would have to say so here and be refused.
    /// </summary>
    public TemporalCoverage Coverage => TemporalCoverage.Complete;

    public async Task<ClassificationResult> ClassifyAsync(
        ClassificationRequest request, CancellationToken cancellationToken = default)
    {
        if (_transcriber is null || _transcriptClassifier is null || _audioClassifier is null)
        {
            // Named individually, because "speech classification unavailable" sends an operator
            // looking in three places at once on the day it matters.
            _logger.LogError(
                "Speech classification is not configured — transcriber: {HasTranscriber}, "
                + "transcript classifier: {HasTranscriptClassifier}, audio classifier: {HasAudioClassifier}.",
                _transcriber is not null, _transcriptClassifier is not null, _audioClassifier is not null);

            // Refuses rather than degrading to whichever leg happens to be available. Half-classified
            // narration is not classified narration, and the gate must be told that plainly.
            throw new InvalidOperationException(
                "Speech classification needs a transcriber, a transcript classifier and an audio "
                + "classifier. Narration is refused until all three are configured — voice is not "
                + "covered by any other modality's classification.");
        }

        // Leg one: what was said.
        var transcript = await _transcriber.TranscribeAsync(
            request.SubjectPartyId, request.Reference, cancellationToken);

        var textResult = await _transcriptClassifier.ClassifyAsync(
            request with { Reference = transcript.Text }, cancellationToken);

        // Leg two: how it was said. Runs on the ORIGINAL audio, not the transcript, which is the
        // entire reason this classifier exists rather than a call to the text one.
        var audioResult = await _audioClassifier.ClassifyAsync(request, cancellationToken);

        return new ClassificationResult(
            Merge(textResult.Scores, audioResult.Scores),
            transcript.RunId,
            [.. textResult.AllRunIds, .. audioResult.AllRunIds]);
    }

    /// <summary>
    /// The higher score per category wins.
    ///
    /// <para>
    /// Averaging would let a clean transcript dilute a distressing delivery below threshold, which is
    /// precisely the case this classifier was built for. Either leg firing blocks.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, double> Merge(
        IReadOnlyDictionary<string, double> text, IReadOnlyDictionary<string, double> audio)
    {
        var merged = new Dictionary<string, double>(text, StringComparer.OrdinalIgnoreCase);

        foreach (var (category, score) in audio)
        {
            merged[category] = merged.TryGetValue(category, out var existing)
                ? Math.Max(existing, score)
                : score;
        }

        return merged;
    }
}
