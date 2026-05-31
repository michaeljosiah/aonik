using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

/// <summary>
/// Persists the validate -> pay handshake: the token, resolved customer name, outstanding amount,
/// resolved fields, and expiry that must carry into payment. The persistent home for a
/// BillCustomerValidationResult so a later PayBill can present the same ValidationToken. Tenant-scoped.
/// </summary>
public class BillValidation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ClientReference { get; set; } = string.Empty;
    public Guid ConnectorId { get; set; }
    public Guid? CatalogBillerServiceId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ValidationToken { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal? OutstandingAmount { get; set; }
    public string? Currency { get; set; }
    public string ResolvedFieldsJson { get; set; } = "{}";
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
