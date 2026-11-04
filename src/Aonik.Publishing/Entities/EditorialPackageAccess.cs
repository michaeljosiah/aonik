using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class EditorialPackageAccess : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ActorPartyId { get; set; }
    public string PackageSha256 { get; set; } = string.Empty;
    public DateTime AccessedAt { get; set; }
    public string AccessKind { get; set; } = "package";
    public string? AssetId { get; set; }
}
