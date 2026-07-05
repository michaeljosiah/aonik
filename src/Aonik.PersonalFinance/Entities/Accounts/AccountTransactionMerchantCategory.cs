using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities.Accounts;

public class AccountTransactionMerchantCategory : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string MerchantKey { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
}
