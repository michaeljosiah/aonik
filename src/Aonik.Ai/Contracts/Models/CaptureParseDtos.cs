namespace Aonik.Ai.Contracts.Models;

// ────────────────────────────────────────────────────────────────────
// Capture-parse contract (Spec 047 §4). An image (receipt / screenshot),
// raw text, or an audio transcript — plus the user's entity + open-commitment
// hints — becomes a structured DRAFT proposal. Never persisted: the review
// screen is mandatory and only a user-confirmed draft becomes a PaymentLog
// (Spec 045). The shapes here are camelCase-symmetrical with the LLM's
// JSON-schema output so the model's response deserialises straight into them.
// ────────────────────────────────────────────────────────────────────

/// <summary>Allowed <see cref="CaptureParseRequest.InputType"/> values.</summary>
public static class CaptureInputTypes
{
    public const string Image = "image";
    public const string Text = "text";
    public const string AudioTranscript = "audioTranscript";

    public static readonly string[] All = [Image, Text, AudioTranscript];
}

/// <summary>Allowed <see cref="CaptureParseResponse.Status"/> values.</summary>
public static class CaptureParseStatuses
{
    /// <summary>A confident draft was produced.</summary>
    public const string Parsed = "parsed";

    /// <summary>A draft was produced but one or more fields are low-confidence — the review UI should highlight them.</summary>
    public const string LowConfidence = "lowConfidence";

    /// <summary>Nothing usable could be extracted; the client falls back to the manual form (pre-filled where possible).</summary>
    public const string Unparseable = "unparseable";
}

/// <summary>
/// Request to <c>POST /ai/capture/parse</c>. <see cref="Payload"/> is a base64
/// image (optionally a <c>data:</c> URI) when <see cref="InputType"/> is
/// <c>image</c>, otherwise raw text. <see cref="Hints"/> turn matching into
/// near-classification: the model picks the entity / commitment from the
/// supplied lists rather than free-forming.
/// </summary>
public record CaptureParseRequest(
    string InputType,
    string Payload,
    CaptureHints? Hints);

/// <summary>The user's own entities and open commitments, supplied to constrain matching.</summary>
public record CaptureHints(
    IReadOnlyList<CaptureEntityHint>? Entities,
    IReadOnlyList<CaptureCommitmentHint>? OpenCommitments);

/// <summary>A candidate care entity (opaque client-side id + display name).</summary>
public record CaptureEntityHint(string Id, string Name);

/// <summary>A candidate open commitment, with its expected amount + due date.</summary>
public record CaptureCommitmentHint(
    string Id,
    string Title,
    CaptureMoney? Expected,
    DateTime? DueDate);

/// <summary>An amount + ISO currency. Currency is recorded, never converted (Simi has no FX).</summary>
public record CaptureMoney(decimal Value, string Currency);

/// <summary>
/// Response from <c>POST /ai/capture/parse</c> — a PROPOSAL, never persisted.
/// <see cref="Draft"/> is <c>null</c> only when <see cref="Status"/> is
/// <c>unparseable</c>. <see cref="AiRunId"/> is the audited AI execution that produced
/// the draft (Spec 047 §8); the client carries it onto the confirmed Spec 045 create so
/// the financial record references the AI run that proposed it.
/// </summary>
public record CaptureParseResponse(
    string Status,
    CaptureDraft? Draft,
    Guid? AiRunId = null);

/// <summary>
/// The structured draft the user reviews before confirming. Any field whose
/// <see cref="FieldConfidence"/> is below 0.7 should be highlighted in the
/// review UI. On confirm the client calls Spec 045's idempotent create.
/// </summary>
public record CaptureDraft(
    string Kind,
    CaptureMatch? EntityMatch,
    CaptureMatch? CommitmentMatch,
    CaptureMoney? Amount,
    DateTime? Date,
    string? Channel,
    string? Note,
    IReadOnlyDictionary<string, double>? FieldConfidence);

/// <summary>A matched hint id with the model's confidence in the match (0..1).</summary>
public record CaptureMatch(string Id, double Confidence);
