using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class Subscription : AuditableEntity, ITenantScoped
{
    public Guid SubscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Merchant { get; private set; } = string.Empty;
    public DateTime RenewalDate { get; private set; }
    public decimal ExpectedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string DetectedBy { get; private set; } = string.Empty;

    private Subscription() { }

    public Subscription(Guid tenantId, Guid userId, string merchant, DateTime renewalDate, decimal expectedAmount, string currency, string detectedBy)
    {
        SubscriptionId = Id;
        TenantId = tenantId;
        UserId = userId;
        Merchant = merchant;
        RenewalDate = renewalDate;
        ExpectedAmount = expectedAmount;
        Currency = currency;
        DetectedBy = detectedBy;
        Status = "Active";
    }

    public void UpdateRenewalDate(DateTime renewalDate)
    {
        RenewalDate = renewalDate;
    }

    public void UpdateExpectedAmount(decimal expectedAmount)
    {
        ExpectedAmount = expectedAmount;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void Cancel()
    {
        Status = "Cancelled";
    }
}
