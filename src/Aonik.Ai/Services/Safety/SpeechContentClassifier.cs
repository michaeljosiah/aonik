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
public interface ISpeechTranscriber : ITemporalCoverage
{
    /// <summary>
    /// Transcribe audio at <paramref name="reference"/>. Throwing is a legitimate outcome: the gate
    /// turns it into <c>CheckUnavailable</c> and the narration is not played.
    /// </summary>
    /// <param name="modelName">
    /// Resolved centrally through <c>AiRoutePolicy</c> and already checked against the subject's
    /// consented providers. An adapter that picks its own vendor would be a second routing mechanism
    /// alongside the platform rule — and, worse, a path that sends a child's audio to a company the
    /// family's terms never named, which is exactly what §16.1 forbids of the classification legs.
    /// </param>
    Task<SpeechTranscript> TranscribeAsync(
        Guid subjectPartyId,
        string reference,
        string modelName,
        CancellationToken cancellationToken = default);
}

/// <param name="RunId">The <c>AiRun</c> for the transcription. Recorded on the decision like any other.</param>
public sealed record SpeechTranscript(string Text, Guid RunId);

/// <summary>
/// A speech leg failed after earlier legs had already run.
///
/// <para>
/// Carries the <c>AiRun</c> ids that <em>did</em> happen, so the gate's <c>CheckUnavailable</c> decision
/// still names them. Throwing a bare exception loses them, and an outage decision disconnected from the
/// AI executions that actually occurred is the audit gap §15 exists to close.
/// </para>
/// </summary>
public sealed class SpeechClassificationFailedException : Exception
{
    public SpeechClassificationFailedException(
        string message, IReadOnlyList<Guid> completedRunIds, Exception? innerException = null)
        : base(message, innerException)
        => CompletedRunIds = completedRunIds;

    public IReadOnlyList<Guid> CompletedRunIds { get; }
}

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
    private readonly ISafetyModelRouter _router;
    private readonly ILogger<SpeechContentClassifier> _logger;

    public SpeechContentClassifier(
        ISpeechTranscriber? transcriber,
        IContentClassifier? transcriptClassifier,
        IContentClassifier? audioClassifier,
        ISafetyModelRouter router,
        ILogger<SpeechContentClassifier> logger)
    {
        _transcriber = transcriber;
        _transcriptClassifier = transcriptClassifier;
        _audioClassifier = audioClassifier;
        _router = router;
        _logger = logger;
    }

    public string Modality => SafetyModalities.Speech;

    /// <summary>
    /// Derived from the legs, never asserted.
    ///
    /// <para>
    /// Hard-coding <see cref="TemporalCoverage.Complete"/> here would let a sampling transcriber or a
    /// sampling audio provider hide behind a wrapper that claims completeness — the gate would accept
    /// it and mint a permit from sampled scores, which is precisely the rule S6 added. A composite is
    /// only as complete as its least complete part, and anything that has not declared its coverage is
    /// treated as sampling.
    /// </para>
    /// </summary>
    public TemporalCoverage Coverage
        => CoverageOf(_transcriber) == TemporalCoverage.Complete
            && CoverageOf(_transcriptClassifier) == TemporalCoverage.Complete
            && CoverageOf(_audioClassifier) == TemporalCoverage.Complete
                ? TemporalCoverage.Complete
                : TemporalCoverage.Sampled;

    /// <summary>
    /// A leg's declared coverage. A missing leg is not complete, and a leg that says nothing is
    /// treated as complete only when its modality is not temporal — the transcript leg classifies
    /// text, which has no gaps to fall between.
    /// </summary>
    private static TemporalCoverage CoverageOf(object? leg) => leg switch
    {
        null => TemporalCoverage.Sampled,
        ITemporalCoverage declared => declared.Coverage,
        IContentClassifier c when !SafetyModalities.IsTemporal(c.Modality) => TemporalCoverage.Complete,
        _ => TemporalCoverage.Sampled,
    };

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

        // Routed centrally, like both classification legs. Transcription sends a child's audio to a
        // third party, so it cannot be the one call that picks its own vendor and skips the consented-
        // provider check (§16.1).
        var route = await _router.ResolveAsync(
            request.SubjectPartyId, SafetyUseCases.TranscribeSpeech, cancellationToken);

        // Leg one: what was said.
        var transcript = await _transcriber.TranscribeAsync(
            request.SubjectPartyId, request.Reference, route.ModelName, cancellationToken);

        if (string.IsNullOrWhiteSpace(transcript.Text))
        {
            // A successful call returning nothing is a normal failure mode for quiet or unintelligible
            // audio — and treating it as a clean text leg means NOBODY classified what was said, while
            // the delivery-characteristics leg alone lets the narration through.
            throw new SpeechClassificationFailedException(
                "Transcription returned no text; what was said has not been classified.",
                [transcript.RunId]);
        }

        var textResult = await ClassifyLegAsync(
            _transcriptClassifier, request with { Reference = transcript.Text },
            [transcript.RunId], "transcript", cancellationToken);

        // Leg two: how it was said. Runs on the ORIGINAL audio, not the transcript, which is the
        // entire reason this classifier exists rather than a call to the text one.
        var audioResult = await ClassifyLegAsync(
            _audioClassifier, request,
            [transcript.RunId, .. textResult.AllRunIds], "audio", cancellationToken);

        return new ClassificationResult(
            Merge(textResult.Scores, audioResult.Scores),
            transcript.RunId,
            [.. textResult.AllRunIds, .. audioResult.AllRunIds]);
    }

    /// <summary>
    /// Runs one leg, converting a failure into an exception that still carries the runs already made.
    /// </summary>
    private static async Task<ClassificationResult> ClassifyLegAsync(
        IContentClassifier classifier,
        ClassificationRequest request,
        IReadOnlyList<Guid> completedRunIds,
        string leg,
        CancellationToken cancellationToken)
    {
        try
        {
            return await classifier.ClassifyAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SpeechClassificationFailedException(
                $"The {leg} leg of speech classification failed.", completedRunIds, ex);
        }
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
        var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, score) in text.Concat(audio))
        {
            // A NaN from one provider would win Math.Max and then fail every `>= threshold`
            // comparison downstream, so an unusable audio score could erase a transcript score that
            // was over the line. A provider that returns nonsense is a provider we cannot use.
            if (!double.IsFinite(score) || score < 0 || score > 1)
            {
                throw new SpeechClassificationFailedException(
                    $"A classifier returned an unusable score for '{category}'; refusing rather than "
                    + "merging it.", []);
            }

            merged[category] = merged.TryGetValue(category, out var existing)
                ? Math.Max(existing, score)
                : score;
        }

        return merged;
    }
}
