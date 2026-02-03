using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class DocumentFile : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string? StorageContainer { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public int? PageIndex { get; set; }
    public string? Side { get; set; }
    public DateTime? CapturedAt { get; set; }
    public string? CapturedBy { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public Document? Document { get; set; }
}
