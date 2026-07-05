using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Accounts;

/// <summary>
/// A file (receipt, statement, document) attached to an account transaction.
/// </summary>
public class AccountTransactionAttachment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TransactionId { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string? StorageContainer { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Sha256 { get; set; }
}
