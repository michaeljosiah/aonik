namespace Aonik.Finance.Contracts.Models.PersonalFinance;

// ── CareEntity CRUD DTOs (Spec 043 §7) ──────────────────────────────

public record CreateCareEntityRequest(
    string Kind,
    string? AssetType,
    string Name,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    IReadOnlyDictionary<string, string>? Attributes);

public record UpdateCareEntityRequest(
    string Name,
    string? AssetType,
    string CountryCode,
    string? Relationship,
    string? Emoji,
    Guid? PhotoDocumentId,
    IReadOnlyDictionary<string, string>? Attributes);

public record CareEntityResponse(
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

// ── Profile projection (Spec 043 §8) ────────────────────────────────

public record CareEntityProfileResponse(
    CareEntityResponse Entity,
    IReadOnlyList<CurrencyTotal> YearTotals,
    IReadOnlyList<CareEntityCommitmentSummary> Commitments,
    IReadOnlyList<CareEntityPaymentLogSummary> RecentLogs,
    IReadOnlyList<CareEntityDocumentRef> Documents);

/// <summary>Per-currency total — money keeps its origin, never converted (§9).</summary>
public record CurrencyTotal(string Currency, decimal Total, int Count);

// Forward-compatible placeholders for the dependent aggregates. Until
// Specs 044/045/046 land, the profile returns these as empty arrays (the
// projection "grows richer as 044–046 land", §8). Prefixed names avoid a
// collision with the richer summary types those specs introduce.
public record CareEntityCommitmentSummary(
    Guid Id,
    string DisplayName,
    string? Frequency,
    decimal? ExpectedAmount,
    string? Currency,
    DateTime? NextDueDate,
    string Status);

public record CareEntityPaymentLogSummary(
    Guid Id,
    decimal Amount,
    string Currency,
    DateTime Date,
    string? Channel);

public record CareEntityDocumentRef(
    Guid DocumentId,
    string? Title,
    string? DocumentType);
