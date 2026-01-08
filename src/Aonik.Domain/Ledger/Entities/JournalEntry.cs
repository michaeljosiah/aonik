using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class JournalEntry : AuditableEntity, ITenantScoped
{
    public Guid JournalEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LedgerId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<JournalEntryLine> Lines { get; set; } = new();
}
