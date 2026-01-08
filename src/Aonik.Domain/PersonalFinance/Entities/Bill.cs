using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class Bill : AuditableEntity, ITenantScoped
{
    public Guid BillId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Payee { get; private set; } = string.Empty;
    public string Frequency { get; private set; } = string.Empty;
    public DateTime NextDueDate { get; private set; }
    public decimal? ExpectedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public bool Autopay { get; private set; }
    public Guid? LinkedInvoiceId { get; private set; }
    public Guid? LinkedOrderId { get; private set; }
    public string Status { get; private set; } = string.Empty;

    private Bill() { }

    public Bill(Guid tenantId, Guid userId, string payee, string frequency, DateTime nextDueDate, string currency, decimal? expectedAmount = null)
    {
        BillId = Id;
        TenantId = tenantId;
        UserId = userId;
        Payee = payee;
        Frequency = frequency;
        NextDueDate = nextDueDate;
        Currency = currency;
        ExpectedAmount = expectedAmount;
        Autopay = false;
        Status = "Active";
    }

    public void UpdateNextDueDate(DateTime nextDueDate)
    {
        NextDueDate = nextDueDate;
    }

    public void UpdateExpectedAmount(decimal expectedAmount)
    {
        ExpectedAmount = expectedAmount;
    }

    public void EnableAutopay()
    {
        Autopay = true;
    }

    public void DisableAutopay()
    {
        Autopay = false;
    }

    public void LinkInvoice(Guid invoiceId)
    {
        LinkedInvoiceId = invoiceId;
    }

    public void LinkOrder(Guid orderId)
    {
        LinkedOrderId = orderId;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }
}
