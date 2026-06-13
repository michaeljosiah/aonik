namespace Aonik.Finance.Contracts.Models.PersonalFinance;

// ── Circle grant / invite DTOs (Spec 048) ───────────────────────────
// Reuses CurrencyTotal, CareEntityResponse, CareEntityPaymentLogSummary,
// CareEntityDocumentRef from the 043/045 models.

public record CreateCircleGrantRequest(
    Guid MemberUserId,
    string Scope,
    IReadOnlyList<Guid>? EntityIds,
    bool NoAmounts);

public record CircleGrantResponse(
    Guid Id,
    Guid OwnerUserId,
    Guid? MemberUserId,
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts,
    string Status,
    DateTime CreatedAt);

public record CreateCircleInviteRequest(
    string Scope,
    IReadOnlyList<Guid>? EntityIds,
    bool NoAmounts,
    string? Channel);

public record CircleInviteResponse(
    Guid Id,
    string Token,
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts,
    string? Channel,
    DateTime ExpiresAt,
    string Status);

/// <summary>The resolved grant for (member → owner) — the input to the visibility filter.</summary>
public record CircleGrantView(
    Guid OwnerUserId,
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts);

/// <summary>Full shared view of an owner's entity (scope=all|entities) — carries amounts.</summary>
public record CircleSharedEntityView(
    CareEntityResponse Entity,
    IReadOnlyList<CurrencyTotal> YearTotals,
    IReadOnlyList<CareEntityPaymentLogSummary> RecentLogs,
    IReadOnlyList<CareEntityDocumentRef> Documents);

/// <summary>
/// Docs-only shared view (scope=docsOnly) — structurally amount-free: it has NO
/// amount, total, log, or commitment field, so there is no amount in the object
/// graph to leak (Spec 048 §5/§10).
/// </summary>
public record CircleSharedDocsView(
    Guid CareEntityId,
    string Name,
    IReadOnlyList<CareEntityDocumentRef> Documents);

/// <summary>Discriminated shared-entity result — exactly one of Full / DocsOnly is non-null.</summary>
public record CircleSharedEntityResult(
    string Scope,
    CircleSharedEntityView? Full,
    CircleSharedDocsView? DocsOnly);

// ── Support Statement (Spec 048 §9) ─────────────────────────────────

public record CareEntityRef(Guid Id, string Name, string Kind, string CountryCode);

public record StatementRow(
    DateTime Date,
    string? Description,
    string Channel,
    decimal Amount,
    string Currency,
    Guid? ReceiptDocumentId,
    bool Corroborated);

public record StatementData(
    CareEntityRef Entity,
    DateTime From,
    DateTime To,
    string? PreparedFor,
    IReadOnlyList<StatementRow> Rows,
    IReadOnlyList<CurrencyTotal> Totals,
    IReadOnlyList<CareEntityDocumentRef> ReceiptAppendix,
    string VerificationCode);
