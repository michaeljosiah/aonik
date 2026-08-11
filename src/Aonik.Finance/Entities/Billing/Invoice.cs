using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Billing;

public class Invoice : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Caller-supplied idempotency key (Spec 088 §8). Null for the many invoices raised without
    /// one; unique per tenant when present, so a retried write returns the original invoice
    /// instead of billing the customer twice.
    /// </summary>
    public string? IdempotencyKey { get; set; }
    public Guid CustomerAccountId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProvenanceJson { get; set; } = string.Empty;

    public List<InvoiceLine> Lines { get; set; } = new();
}
