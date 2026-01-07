using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class InvoiceLineItem : Entity
{
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    private InvoiceLineItem() { }

    public InvoiceLineItem(Guid invoiceId, string description, decimal quantity, decimal unitPrice)
    {
        InvoiceId = invoiceId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = quantity * unitPrice;
    }
}
