using Aonik.SharedKernel.Primitives;

namespace Aonik.Publishing.Entities;

/// <summary>A creator's immutable declaration. Package claims are unverified until a trusted validator runs.</summary>
public class SubmissionRevision : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public int Number { get; set; }
    public string RequestDigest { get; set; } = string.Empty;
    public int RequestDigestVersion { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string CreatorDisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string PackageFileName { get; set; } = string.Empty;
    public long DeclaredPackageBytes { get; set; }
    public string DeclaredPackageSha256 { get; set; } = string.Empty;
    public string RightsDeclarationVersion { get; set; } = string.Empty;
    public bool DistributionPermissionGranted { get; set; }
    public string? EditorialMaterialJson { get; set; }
    public Guid? OriginatingUserId { get; set; }
    public Guid? RevisionOperationId { get; set; }
    public string? RevisionCommandDigest { get; set; }
    public Guid? CurrentUploadSessionId { get; set; }
    public Guid? CurrentValidationReportId { get; set; }
    public DateTime? UploadedAt { get; set; }
    public DateTime? PackageVerifiedAt { get; set; }
}
