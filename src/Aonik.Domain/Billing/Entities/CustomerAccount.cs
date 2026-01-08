using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class CustomerAccount : AuditableEntity
{
    public Guid CustomerAccountId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid MerchantPartyId { get; private set; }
    public Guid CustomerPartyId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string PreferencesJson { get; private set; } = string.Empty;

    private CustomerAccount() { }

    public CustomerAccount(Guid tenantId, Guid merchantPartyId, Guid customerPartyId)
    {
        CustomerAccountId = Id;
        TenantId = tenantId;
        MerchantPartyId = merchantPartyId;
        CustomerPartyId = customerPartyId;
        Status = "Active";
        PreferencesJson = "{}";
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void UpdatePreferences(string preferencesJson)
    {
        PreferencesJson = preferencesJson;
    }

    public void Suspend()
    {
        Status = "Suspended";
    }

    public void Activate()
    {
        Status = "Active";
    }

    public void Close()
    {
        Status = "Closed";
    }
}
