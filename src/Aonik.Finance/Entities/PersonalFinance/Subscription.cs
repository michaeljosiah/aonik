using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class Subscription : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public DateTime RenewalDate { get; set; }
    public decimal ExpectedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
}
