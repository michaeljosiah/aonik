using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class InvoiceLine : AuditableEntity, ITenantScoped
{
    public Guid InvoiceLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal LineTotal { get; private set; }
    public string MetadataJson { get; private set; } = string.Empty;

    private InvoiceLine() { }

    public InvoiceLine(Guid tenantId, Guid invoiceId, string description, decimal quantity, decimal unitPrice, decimal taxRate = 0)
    {
        InvoiceLineId = Id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        LineTotal = quantity * unitPrice;
        MetadataJson = "{}";
    }

    public void UpdateQuantity(decimal quantity)
    {
        Quantity = quantity;
        LineTotal = quantity * UnitPrice;
    }

    public void UpdateUnitPrice(decimal unitPrice)
    {
        UnitPrice = unitPrice;
        LineTotal = Quantity * unitPrice;
    }

    public void UpdateMetadata(string metadataJson)
    {
        MetadataJson = metadataJson;
    }
}
