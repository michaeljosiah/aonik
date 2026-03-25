using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class FinancialContext : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContextType { get; set; } = string.Empty;
    public Guid? RelatedPartyId { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    public string MetadataJson { get; set; } = "{}";

    public List<FinancialContextFundingSource> FundingSources { get; set; } = new();
}
