using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class PaymentIntent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid PayerPartyId { get; set; }
    public Guid? PayeePartyId { get; set; }
    public string PurposeType { get; set; } = string.Empty;
    public Guid PurposeId { get; set; }
    public string PaymentMethodType { get; set; } = string.Empty;
    public string? PaymentMethodRef { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}
