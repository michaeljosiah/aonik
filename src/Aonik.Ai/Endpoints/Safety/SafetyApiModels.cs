namespace Aonik.Ai.Endpoints.Safety;

// The wire shapes of the content-safety endpoints (Spec 096 §7, §8; aonik#323 for ArkeKidz#10): a
// product screens what a child typed before a model sees it, screens what came back before the
// child does, and later proves that what it is about to deliver is exactly what was judged.

/// <param name="SubjectPartyId">The child the content is for; the age band and consent are theirs.</param>
/// <param name="Modality">Text, image or generated speech.</param>
/// <param name="Layer"><c>input</c> (what the child typed) or <c>output</c> (what a model produced).</param>
/// <param name="Content">Text or inline image/audio bytes, classified and hashed before delivery.</param>
public sealed record ScreenContentRequest(
    Guid SubjectPartyId,
    string Modality,
    string Layer,
    string Content,
    Guid? GenerationRunId = null,
    Guid? UsageReservationId = null);

/// <param name="Outcome"><c>allowed</c>, <c>blocked</c>, <c>held-for-review</c>, <c>check-unavailable</c> or <c>modality-disabled</c>.</param>
/// <param name="ContentHash">SHA-256 of the exact content judged, lowercase hex: what a delivery check compares against.</param>
/// <param name="PendingReviewId">Set when the outcome is <c>held-for-review</c>: what a guardian approves or declines.</param>
public sealed record SafetyVerdictResponse(
    Guid DecisionId,
    bool Allowed,
    string Outcome,
    IReadOnlyList<string> Categories,
    string ContentHash,
    string SafetyBand,
    string PolicyVersion,
    Guid? PendingReviewId);

public sealed record SafetyDecisionResponse(
    Guid DecisionId,
    Guid SubjectPartyId,
    string SafetyBand,
    string Modality,
    string Layer,
    string Outcome,
    IReadOnlyList<string> Categories,
    string PolicyVersion,
    string? ContentHash,
    DateTime DecidedAt,
    PendingReviewModel? Review);

/// <param name="State"><c>pending</c>, <c>approved</c>, <c>declined</c> or <c>expired</c>.</param>
public sealed record PendingReviewModel(Guid PendingReviewId, string State, DateTime HeldAt, DateTime ExpiresAt, Guid? DecidedByPartyId, DateTime? DecidedAt);

/// <param name="Decision"><c>approve</c> or <c>decline</c>.</param>
public sealed record ReviewDecisionRequest(string Decision);

/// <param name="Outcome"><c>approved</c>, <c>declined</c> or <c>not-available</c> (already decided, expired, or no such review).</param>
public sealed record ReviewDecisionResponse(Guid DecisionId, string Outcome, string? ContentHash);

/// <summary>Every refusal these endpoints make on purpose: a stable code and a message for a log.</summary>
public sealed record SafetyProblem(int Status, string Code, string Message);
