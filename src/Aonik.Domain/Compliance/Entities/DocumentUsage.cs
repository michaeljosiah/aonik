using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class DocumentUsage : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid OwnerPartyId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? Notes { get; set; }
    public Document? Document { get; set; }
    public List<DocumentVerification> Verifications { get; set; } = new();
}
