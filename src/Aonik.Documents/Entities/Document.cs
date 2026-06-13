using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Compliance;

public class Document : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OwnerPartyId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Optional display title for the Vault tag sheet (Spec 046). Null for legacy documents.</summary>
    public string? Title { get; set; }

    // Spec 035 §9/§10 — generic-document RAG + classification fields. Existing rows
    // default to Internal/NotIndexable (see DocumentConfiguration) so legacy compliance
    // evidence is never auto-indexed until reclassified by a backfill.
    public DocumentClassification Classification { get; set; } = DocumentClassification.Internal;
    public string Source { get; set; } = "AdminUpload";
    public DocumentIndexStatus IndexStatus { get; set; } = DocumentIndexStatus.NotIndexable;
    public DateTime? IndexedAt { get; set; }

    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? IssuerName { get; set; }
    public string? CountryCode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string AttributesJson { get; set; } = "{}";
    public List<DocumentFile> Files { get; set; } = new();
    public List<DocumentVersion> Versions { get; set; } = new();
}
