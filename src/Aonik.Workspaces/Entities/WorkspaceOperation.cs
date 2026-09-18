using Aonik.SharedKernel.Primitives;

namespace Aonik.Workspaces.Entities;

/// <summary>
/// One engine operation against a workspace, as the Arke engine's operation store needs it kept
/// (aonik#327): inserted once by key, completed once, never deleted.
///
/// <para>
/// The key is the engine's own — a hash over the trusted scope, actor, world and the client's
/// operation id — so the same request from the same person is the same row whatever host asks,
/// and a second host asking finds the truth rather than starting the work again. The fingerprint
/// is the request's immutable identity: a key reused for different content is refused. A row that
/// is <c>started</c> and never completed is a durable "uncertain": the work may have happened, and
/// the product reconciles it against the record, the jobs and the reservations before deciding.
/// </para>
/// </summary>
public class WorkspaceOperation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>The engine's operation key: lowercase hex SHA-256.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The engine's fingerprint of the request: action, resource, input, subject.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>The engine action, e.g. <c>propose</c>, <c>accept</c>, <c>chapter-draft</c>, <c>generate</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The party who asked, as the product resolved them. Kept for the audit; access is the workspace's.</summary>
    public Guid CallerPartyId { get; set; }

    /// <summary><c>started</c> or <c>completed</c> (<see cref="WorkspaceOperationStatuses"/>).</summary>
    public string Status { get; set; } = WorkspaceOperationStatuses.Started;

    /// <summary>The engine's trusted context, verbatim: actor, scope, executor, subject.</summary>
    public string ContextJson { get; set; } = string.Empty;

    /// <summary>The engine's resource, verbatim: world and whatever it names within it.</summary>
    public string ResourceJson { get; set; } = string.Empty;

    /// <summary>
    /// The engine's result, verbatim, once completed — or, for some operations, a result carried
    /// from the start (the engine begins a settlement with the decision it is about to act on).
    /// Reservation ids and job ids live here, which is the stable link to those records.
    /// </summary>
    public string? ResultJson { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public static class WorkspaceOperationStatuses
{
    public const string Started = "started";
    public const string Completed = "completed";
}
