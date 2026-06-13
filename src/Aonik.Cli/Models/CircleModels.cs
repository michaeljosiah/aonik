namespace Aonik.Cli.Models;

// ── Circle API contracts (Spec 048) ─────────────────────────────────
// CurrencyTotal + CareEntityDocumentRef are defined in CareEntityModels.cs.

public sealed record CreateCircleGrantRequest(
    Guid MemberUserId,
    string Scope,
    IReadOnlyList<Guid>? EntityIds,
    bool NoAmounts);

public sealed record CircleGrantResponse(
    Guid Id,
    Guid OwnerUserId,
    Guid? MemberUserId,
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts,
    string Status,
    DateTime CreatedAt);

public sealed record CreateCircleInviteRequest(
    string Scope,
    IReadOnlyList<Guid>? EntityIds,
    bool NoAmounts,
    string? Channel);

public sealed record CircleInviteResponse(
    Guid Id,
    string Token,
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts,
    string? Channel,
    DateTime ExpiresAt,
    string Status);

public sealed record CareEntityRef(Guid Id, string Name, string Kind, string CountryCode);

public sealed record StatementRow(
    DateTime Date,
    string? Description,
    string Channel,
    decimal Amount,
    string Currency,
    Guid? ReceiptDocumentId,
    bool Corroborated);

public sealed record StatementData(
    CareEntityRef Entity,
    DateTime From,
    DateTime To,
    string? PreparedFor,
    IReadOnlyList<StatementRow> Rows,
    IReadOnlyList<CurrencyTotal> Totals,
    IReadOnlyList<CareEntityDocumentRef> ReceiptAppendix,
    string VerificationCode);

// ── Command options ─────────────────────────────────────────────────

public sealed record CreateCircleGrantOptions(
    Guid MemberUserId,
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts,
    OutputMode OutputMode);

public sealed record CreateCircleInviteOptions(
    string Scope,
    IReadOnlyList<Guid> EntityIds,
    bool NoAmounts,
    string? Channel,
    OutputMode OutputMode);
