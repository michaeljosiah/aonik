using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Compliance;

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

    // Spec 035 — DocumentId is a cross-module reference resolved via IDocumentReader; the EF
    // navigation to Document (now in Aonik.Documents) is intentionally absent (no FK).
    public List<DocumentVerification> Verifications { get; set; } = new();
}
