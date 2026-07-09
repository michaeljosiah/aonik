namespace Aonik.PersonalFinance.Contracts.Models;

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

/// <summary>
/// The anonymous, amount-free headline of an invite (Spec 061 §5) — enough to render
/// "someone shared X with you" before sign-up, nothing more. It carries NO amount,
/// balance, corroboration, member list, or document content: the preview is a headline,
/// not a view. <see cref="EntityNames"/> is the one deliberate disclosure (governed by the
/// <c>InvitePreviewDisclosure</c> switch); it is empty for scope=all and in Counts mode,
/// and never carries amounts.
/// </summary>
public record InvitePreviewResponse(
    string OwnerDisplayName,
    string Scope,
    string ScopeLabel,
    IReadOnlyList<string> EntityNames,
    int EntityCount,
    bool NoAmounts,
    DateTime ExpiresAt);

/// <summary>
/// The outcome of an invite accept (Spec 061 §7). The endpoint maps it to a status code so the
/// three cases stay distinct: a bound grant is 200, an invalid/spent token is a fail-closed 404,
/// and an owner accepting their own invite is a 409 (a state conflict, not a bad token).
/// </summary>
public enum AcceptInviteStatus
{
    /// <summary>Bound, or already bound by this same user (idempotent) — <see cref="AcceptInviteResult.Grant"/> is set. → 200.</summary>
    Accepted,

    /// <summary>Token invalid, expired, or already consumed by a different user (fail-closed, no oracle). → 404.</summary>
    Invalid,

    /// <summary>The caller is the invite's owner — you cannot be a member of your own circle. → 409.</summary>
    SelfAccept,
}

/// <summary>Discriminated accept result — <see cref="Grant"/> is non-null iff <see cref="Status"/> is Accepted.</summary>
public record AcceptInviteResult(AcceptInviteStatus Status, CircleGrantResponse? Grant)
{
    public static AcceptInviteResult FromGrant(CircleGrantResponse grant) => new(AcceptInviteStatus.Accepted, grant);
    public static readonly AcceptInviteResult Invalid = new(AcceptInviteStatus.Invalid, null);
    public static readonly AcceptInviteResult SelfAccept = new(AcceptInviteStatus.SelfAccept, null);
}

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

/// <summary>
/// A member's paged view of one shared entity's expenses — the full list behind the entity
/// view's recent-log preview (Spec 048). Each row carries the expense's corroboration status.
/// Only ever returned to a member with amount access (scope=all|entities, NoAmounts=false);
/// a docsOnly / no-amounts member gets 404, never this projection, so the no-amounts property holds.
/// </summary>
public record CircleSharedPaymentLogsResult(
    IReadOnlyList<CareEntityPaymentLogSummary> Items,
    int Page,
    int PageSize,
    bool HasMore);

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
