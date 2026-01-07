using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class Invoice : Entity
{
    public Guid CustomerId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTime IssuedUtc { get; private set; }
    public DateTime DueUtc { get; private set; }

    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private Invoice() { }

    public Invoice(Guid customerId, string invoiceNumber, string currency, DateTime dueUtc)
    {
        CustomerId = customerId;
        InvoiceNumber = invoiceNumber;
        Currency = currency;
        Status = InvoiceStatus.Draft;
        IssuedUtc = DateTime.UtcNow;
        DueUtc = dueUtc;
        TotalAmount = 0;
    }

    public void AddLineItem(InvoiceLineItem lineItem)
    {
        _lineItems.Add(lineItem);
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        TotalAmount = _lineItems.Sum(x => x.LineTotal);
    }

    public void MarkAsIssued()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be issued");

        Status = InvoiceStatus.Issued;
    }

    public void MarkAsPaid()
    {
        if (Status != InvoiceStatus.Issued)
            throw new InvalidOperationException("Only issued invoices can be marked as paid");

        Status = InvoiceStatus.Paid;
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("Paid invoices cannot be cancelled");

        Status = InvoiceStatus.Cancelled;
    }
}
