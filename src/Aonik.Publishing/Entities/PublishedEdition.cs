using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublishedEdition : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ChannelKey { get; set; } = "";
    public string WorldId { get; set; } = "";
    public string Slot { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public Guid CreatorPartyId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid? RevisionId { get; set; }
    public Guid? DecisionId { get; set; }
    public Guid OperationId { get; set; }
    public Guid SnapshotId { get; set; }
    public string PackageSha256 { get; set; } = "";
    public string? Fingerprint { get; set; }
    public string? BaseFingerprint { get; set; }
    public string PinJson { get; set; } = "";
    public bool IsLegacy { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTime? WithdrawnAt { get; set; }
}
