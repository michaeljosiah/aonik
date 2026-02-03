using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class Document : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OwnerPartyId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? IssuerName { get; set; }
    public string? CountryCode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string AttributesJson { get; set; } = "{}";
    public List<DocumentFile> Files { get; set; } = new();
    public List<DocumentUsage> Usages { get; set; } = new();
    public List<DocumentVersion> Versions { get; set; } = new();
}
