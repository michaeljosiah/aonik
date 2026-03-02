using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class StatementImport : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PersonalAccountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RowsTotal { get; set; }
    public int RowsParsed { get; set; }
    public int RowsImported { get; set; }
    public int RowsDuplicate { get; set; }
    public int RowsFailed { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
