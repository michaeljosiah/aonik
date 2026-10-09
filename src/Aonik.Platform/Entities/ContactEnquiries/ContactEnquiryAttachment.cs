using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.ContactEnquiries;

public class ContactEnquiryAttachment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EnquiryId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}
