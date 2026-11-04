using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class EditorialDecision : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid OperationId { get; set; }
    public string CommandDigest { get; set; } = string.Empty;
    public string ReviewDigest { get; set; } = string.Empty;
    public string ExpectedVersion { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string InternalReason { get; set; } = string.Empty;
    public string CreatorMessage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public Guid ActorPartyId { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public string? ChecksJson { get; set; }
    public DateTime DecidedAt { get; set; }
}
