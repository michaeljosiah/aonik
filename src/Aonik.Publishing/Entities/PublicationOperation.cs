using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationOperation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ChannelKey { get; set; } = "";
    public Guid ClientOperationId { get; set; }
    public string CommandDigest { get; set; } = "";
    public string Kind { get; set; } = "publish";
    public Guid? SubmissionId { get; set; }
    public Guid? RevisionId { get; set; }
    public Guid? DecisionId { get; set; }
    public Guid? EditionId { get; set; }
    public Guid? ParentOperationId { get; set; }
    public string ReviewDigest { get; set; } = "";
    public string ExpectedVersion { get; set; } = "";
    public long ExpectedSerial { get; set; }
    public string? ExpectedBaseFingerprint { get; set; }
    public string? ExpectedSlotFingerprint { get; set; }
    public bool ConfirmBaseReplacement { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorPartyId { get; set; }
    public string Reason { get; set; } = "";
    public string PolicyJson { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string Code { get; set; } = "queued";
    public Guid? ClaimToken { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public int Attempts { get; set; }
    public int RetryCycles { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? CommittedSerial { get; set; }
    public Guid? CommittedSnapshotId { get; set; }
}
