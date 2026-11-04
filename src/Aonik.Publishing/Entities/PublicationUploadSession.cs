using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationUploadSession : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CreatorPartyId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RevisionId { get; set; }
    public string StorageLocationId { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public long DeclaredBytes { get; set; }
    public string DeclaredSha256 { get; set; } = string.Empty;
    public int PartSizeBytes { get; set; }
    public int TotalParts { get; set; }
    public long ReceivedBytes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid? AssemblyToken { get; set; }
    public DateTime? AssemblyExpiresAt { get; set; }
    public bool ReservationReleased { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid? RetiredByUserId { get; set; }
    public DateTime? CleanedAt { get; set; }
}
