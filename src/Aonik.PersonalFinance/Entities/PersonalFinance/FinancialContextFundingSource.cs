using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class FinancialContextFundingSource : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid FinancialContextId { get; set; }
    public Guid PersonalAccountId { get; set; }
    public bool IsPrimary { get; set; }
}
