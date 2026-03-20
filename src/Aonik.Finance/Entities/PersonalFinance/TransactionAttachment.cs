using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

/// <summary>
/// A file (receipt, photo, document) attached to a personal transaction.
/// Storage metadata mirrors the <c>DocumentFile</c> pattern from the
/// Platform Compliance module.
/// </summary>
public class TransactionAttachment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid TransactionId { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string? StorageContainer { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Sha256 { get; set; }

    public PersonalTransaction? Transaction { get; set; }
}
