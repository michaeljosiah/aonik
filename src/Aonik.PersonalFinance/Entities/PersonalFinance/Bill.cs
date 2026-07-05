using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class Bill : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PaidFromAccountId { get; set; }
    public string Payee { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime NextDueDate { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool Autopay { get; set; }
    public Guid? LinkedInvoiceId { get; set; }
    public Guid? LinkedOrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}
