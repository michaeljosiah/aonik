using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Ledger;

public class JournalEntry : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid LedgerId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<JournalEntryLine> Lines { get; set; } = new();
}
