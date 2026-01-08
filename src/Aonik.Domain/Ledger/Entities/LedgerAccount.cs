using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class LedgerAccount : AuditableEntity, ITenantScoped
{
    public Guid LedgerAccountId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LedgerId { get; private set; }
    public string AccountType { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string DimensionsJson { get; private set; } = string.Empty;

    private LedgerAccount() { }

    public LedgerAccount(Guid tenantId, Guid ledgerId, string accountType, string name, string code)
    {
        LedgerAccountId = Id;
        TenantId = tenantId;
        LedgerId = ledgerId;
        AccountType = accountType;
        Name = name;
        Code = code;
        DimensionsJson = "{}";
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateCode(string code)
    {
        Code = code;
    }

    public void UpdateDimensions(string dimensionsJson)
    {
        DimensionsJson = dimensionsJson;
    }
}
