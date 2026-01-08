using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class Invoice : AuditableEntity
{
    public Guid InvoiceId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CustomerAccountId { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal Subtotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal Total { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string ProvenanceJson { get; private set; } = string.Empty;

    private readonly List<InvoiceLine> _lines = new();
    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

    private Invoice() { }

    public Invoice(Guid tenantId, Guid customerAccountId, DateTime dueDate, string currency)
    {
        InvoiceId = Id;
        TenantId = tenantId;
        CustomerAccountId = customerAccountId;
        IssueDate = DateTime.UtcNow;
        DueDate = dueDate;
        Currency = currency;
        Status = "Draft";
        ProvenanceJson = "{}";
        Subtotal = 0;
        TaxTotal = 0;
        DiscountTotal = 0;
        Total = 0;
    }

    public void AddLine(InvoiceLine line)
    {
        _lines.Add(line);
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        Subtotal = _lines.Sum(x => x.LineTotal);
        TaxTotal = _lines.Sum(x => x.LineTotal * x.TaxRate);
        Total = Subtotal + TaxTotal - DiscountTotal;
    }

    public void ApplyDiscount(decimal discountTotal)
    {
        DiscountTotal = discountTotal;
        RecalculateTotals();
    }

    public void Issue()
    {
        if (Status != "Draft")
            throw new InvalidOperationException("Only draft invoices can be issued");

        Status = "Issued";
    }

    public void MarkAsPaid()
    {
        if (Status != "Issued")
            throw new InvalidOperationException("Only issued invoices can be marked as paid");

        Status = "Paid";
    }

    public void Cancel()
    {
        if (Status == "Paid")
            throw new InvalidOperationException("Paid invoices cannot be cancelled");

        Status = "Cancelled";
    }
}
