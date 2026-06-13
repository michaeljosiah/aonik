namespace Aonik.Cli.Models;

// ── Capture-parse (Spec 047) — CLI-side mirrors of the /ai/capture/parse contract.

public sealed record CaptureParseRequest(
    string InputType,
    string Payload,
    CaptureHints? Hints);

public sealed record CaptureHints(
    IReadOnlyList<CaptureEntityHint>? Entities,
    IReadOnlyList<CaptureCommitmentHint>? OpenCommitments);

public sealed record CaptureEntityHint(string Id, string Name);

public sealed record CaptureCommitmentHint(
    string Id,
    string Title,
    CaptureMoney? Expected,
    DateTime? DueDate);

public sealed record CaptureMoney(decimal Value, string Currency);

public sealed record CaptureParseResponse(
    string Status,
    CaptureDraft? Draft);

public sealed record CaptureDraft(
    string Kind,
    CaptureMatch? EntityMatch,
    CaptureMatch? CommitmentMatch,
    CaptureMoney? Amount,
    DateTime? Date,
    string? Channel,
    string? Note,
    IReadOnlyDictionary<string, double>? FieldConfidence);

public sealed record CaptureMatch(string Id, double Confidence);

/// <summary>Options for <c>capture parse</c>. Exactly one of <see cref="Text"/> / <see cref="ImagePath"/> is supplied.</summary>
public sealed record CaptureParseOptions(
    string InputType,
    string? Text,
    string? ImagePath,
    string? HintsJson,
    OutputMode OutputMode);
