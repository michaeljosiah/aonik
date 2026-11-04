using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

public class PublicationValidationReport : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid OperationId { get; set; }
    public string Status { get; set; } = "pending";
    public string Code { get; set; } = "queued";
    public string PackageSha256 { get; set; } = string.Empty;
    public long PackageBytes { get; set; }
    public string PolicyJson { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public string ReaderCommit { get; set; } = string.Empty;
    public string VerifierImage { get; set; } = string.Empty;
    public Guid? ClaimToken { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
