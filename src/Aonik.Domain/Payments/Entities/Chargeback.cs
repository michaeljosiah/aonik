using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class Chargeback : AuditableEntity, ITenantScoped
{
    public Guid ChargebackId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
}
