namespace Aonik.Cli.Models;

// ── CareEntity API contracts (Spec 043) ─────────────────────────────

public sealed record CreateCareEntityRequest(
    string Kind,
    string? AssetType,
    string Name,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record UpdateCareEntityRequest(
    string Name,
    string? AssetType,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record CareEntityResponse(
    Guid Id,
    string Kind,
    string? AssetType,
    string Name,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    IReadOnlyDictionary<string, string> Attributes,
    bool Archived,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CurrencyTotal(string Currency, decimal Total, int Count);

public sealed record CareEntityCommitmentSummary(
    Guid Id,
    string DisplayName,
    string? Frequency,
    decimal? ExpectedAmount,
    string? Currency,
    DateTime? NextDueDate,
    string Status);

public sealed record CareEntityPaymentLogSummary(
    Guid Id,
    decimal Amount,
    string Currency,
    DateTime Date,
    string? Channel,
    // Reconciliation state of the expense — none | matched | confirmed (Spec 045 §6).
    string CorroborationStatus);

public sealed record CareEntityDocumentRef(
    Guid DocumentId,
    string? Title,
    string? DocumentType);

public sealed record CareEntityProfileResponse(
    CareEntityResponse Entity,
    IReadOnlyList<CurrencyTotal> YearTotals,
    IReadOnlyList<CareEntityCommitmentSummary> Commitments,
    IReadOnlyList<CareEntityPaymentLogSummary> RecentLogs,
    IReadOnlyList<CareEntityDocumentRef> Documents);

// ── Command options ─────────────────────────────────────────────────

public sealed record ListCareEntitiesOptions(
    string? Kind,
    string? AssetType,
    bool IncludeArchived,
    OutputMode OutputMode);

public sealed record CreateCareEntityOptions(
    string Kind,
    string? AssetType,
    string Name,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    string? AttributesFile,
    OutputMode OutputMode);

public sealed record UpdateCareEntityOptions(
    Guid Id,
    string Name,
    string? AssetType,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    string? AttributesFile,
    OutputMode OutputMode);
