using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Spec 096 S5 — the delivered-voice path, and the only sanctioned way generated narration reaches a
/// child.
///
/// <para>
/// The ship condition for this phase is not "a speech classifier exists" but <strong>generated speech
/// is classified before a child hears it</strong> — which is a property of the delivery path, not of
/// the classifier. So the path is expressed in types: the thing a player accepts is a
/// <see cref="PlayableNarration"/>, and one cannot be constructed without a
/// <see cref="ContentDeliveryPermit"/>, which only <see cref="IContentSafetyGate"/> mints.
/// </para>
/// </summary>
public interface IChildNarrationService
{
    /// <summary>
    /// Screen synthesised narration and return something playable, or nothing.
    ///
    /// <para>
    /// A classifier outage returns <see cref="NarrationOutcome.Narration"/> as null with
    /// <see cref="NarrationOutcome.WasUnavailable"/> set — the narration is <em>not</em> played, and
    /// the operator is paged. Passing audio through on a bad day is the specific defect this refuses.
    /// </para>
    /// </summary>
    Task<NarrationOutcome> PrepareAsync(
        NarrationRequest request,
        CancellationToken cancellationToken = default);
}

/// <param name="AudioReference">Where the synthesised audio is. Never the audio itself.</param>
/// <param name="GenerationRunId">The synthesis run, so the decision is traceable to what produced it.</param>
/// <remarks>
/// Carries no safety band. The gate resolves it from the subject's record, so this path cannot be
/// handed <c>adult</c> for a six-year-old.
/// </remarks>
public sealed record NarrationRequest(
    Guid SubjectPartyId,
    string AudioReference,
    Guid? GenerationRunId = null,
    Guid? UsageReservationId = null);

/// <param name="Narration">Non-null only when the gate issued a permit. Absence is the enforcement.</param>
public sealed record NarrationOutcome(
    PlayableNarration? Narration,
    Guid DecisionId,
    SafetyDecisionOutcome Outcome)
{
    /// <summary>
    /// The check could not be performed. <strong>Operationally an outage that must page</strong> —
    /// and distinguishable from a block, because a family whose narration is silently failing and a
    /// family whose story was judged unsafe need different responses.
    /// </summary>
    public bool WasUnavailable => Outcome == SafetyDecisionOutcome.CheckUnavailable;
}

/// <summary>
/// Audio a child may actually hear.
///
/// <para>
/// The constructor is public, and that is not a weakness: it takes a
/// <see cref="ContentDeliveryPermit"/>, which no caller can fabricate. A player that accepts this type
/// cannot be handed unclassified audio, because there is no way to make one. It is the permit trick
/// applied one level down, at the point where bytes actually reach a child.
/// </para>
/// </summary>
public sealed class PlayableNarration
{
    public PlayableNarration(ContentDeliveryPermit permit, string audioReference)
    {
        ArgumentNullException.ThrowIfNull(permit);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioReference);

        if (!permit.Authorises(SafetyModalities.Speech, audioReference))
        {
            // A permit alone was never enough. Any valid one could otherwise be paired with a
            // different reference — or with an image permit — and unclassified audio laundered
            // through a type that looks checked. The permit must cover THIS audio.
            throw new ArgumentException(
                "The permit does not cover this audio. A permit authorises one artefact in one "
                + "modality, and pairing it with anything else would deliver unclassified content.",
                nameof(permit));
        }

        Permit = permit;
        AudioReference = audioReference;
    }

    /// <summary>Proof this audio was classified, carrying the decision it came from.</summary>
    public ContentDeliveryPermit Permit { get; }

    public string AudioReference { get; }

    public Guid DecisionId => Permit.DecisionId;

    public Guid SubjectPartyId => Permit.SubjectPartyId;
}

internal sealed class ChildNarrationService : IChildNarrationService
{
    private readonly IContentSafetyGate _gate;
    private readonly ILogger<ChildNarrationService> _logger;

    public ChildNarrationService(IContentSafetyGate gate, ILogger<ChildNarrationService> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    public async Task<NarrationOutcome> PrepareAsync(
        NarrationRequest request, CancellationToken cancellationToken = default)
    {
        // Screened as SPEECH, not as the text it was synthesised from. Classifying the script and
        // calling the audio covered is the mistake this phase exists to avoid: the script passed, the
        // performance is what reaches the child, and only one of those was judged.
        var verdict = await _gate.ScreenOutputAsync(
            new SafetyRequest(
                request.SubjectPartyId,
                SafetyModalities.Speech,
                request.GenerationRunId,
                request.UsageReservationId),
            new GeneratedContent(SafetyModalities.Speech, request.AudioReference),
            cancellationToken);

        if (verdict.Permit is null)
        {
            if (verdict.WasUnavailable)
            {
                _logger.LogError(
                    "Speech classification unavailable for subject {SubjectId}; narration withheld. "
                    + "This is an outage, not a content decision.", request.SubjectPartyId);
            }

            return new NarrationOutcome(null, verdict.DecisionId, verdict.Outcome);
        }

        return new NarrationOutcome(
            new PlayableNarration(verdict.Permit, request.AudioReference),
            verdict.DecisionId,
            verdict.Outcome);
    }
}
