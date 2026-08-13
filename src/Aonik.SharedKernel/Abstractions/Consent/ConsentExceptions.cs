namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// Thrown when a gated operation is attempted without an active grant (Spec 095 §12.1). The gate
/// fails closed, so this is the ordinary path for missing consent rather than an exceptional one.
/// </summary>
public sealed class ConsentRequiredException : Exception
{
    public ConsentRequiredException(Guid subjectPartyId, string purpose)
        : base($"No active consent for purpose '{purpose}' on subject {subjectPartyId}.")
    {
        SubjectPartyId = subjectPartyId;
        Purpose = purpose;
    }

    public Guid SubjectPartyId { get; }
    public string Purpose { get; }
}

/// <summary>
/// Thrown when a caller attempts to act for a party they hold no guardian authority over.
/// </summary>
public sealed class GuardianAuthorityRequiredException : Exception
{
    public GuardianAuthorityRequiredException(Guid callerPartyId, Guid subjectPartyId)
        : base($"Party {callerPartyId} holds no active guardian authority over {subjectPartyId}.")
    {
        CallerPartyId = callerPartyId;
        SubjectPartyId = subjectPartyId;
    }

    public Guid CallerPartyId { get; }
    public Guid SubjectPartyId { get; }
}
