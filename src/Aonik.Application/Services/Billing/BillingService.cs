using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Billing;
using Aonik.Domain.Billing.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Billing;

public class BillingService : IBillingService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public BillingService(IAonikDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerAccountId = request.CustomerId,
            IssueDate = DateTime.UtcNow,
            DueDate = request.DueUtc,
            Currency = request.Currency,
            Status = "Draft",
            ProvenanceJson = "{}",
            Subtotal = 0,
            TaxTotal = 0,
            DiscountTotal = 0,
            Total = 0,
            Lines = new List<InvoiceLine>()
        };

        foreach (var lineItemRequest in request.LineItems)
        {
            var lineTotal = lineItemRequest.Quantity * lineItemRequest.UnitPrice;

            var lineItem = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InvoiceId = invoice.Id,
                Description = lineItemRequest.Description,
                Quantity = lineItemRequest.Quantity,
                UnitPrice = lineItemRequest.UnitPrice,
                TaxRate = 0,
                LineTotal = lineTotal,
                MetadataJson = "{}"
            };

            invoice.Lines.Add(lineItem);
        }

        RecalculateInvoiceTotals(invoice);

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        return invoice == null ? null : MapToResponse(invoice);
    }

    public async Task AddLineToInvoiceAsync(Guid invoiceId, CreateInvoiceLineItemRequest lineRequest, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        var lineTotal = lineRequest.Quantity * lineRequest.UnitPrice;

        var lineItem = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            Description = lineRequest.Description,
            Quantity = lineRequest.Quantity,
            UnitPrice = lineRequest.UnitPrice,
            TaxRate = 0,
            LineTotal = lineTotal,
            MetadataJson = "{}"
        };

        invoice.Lines.Add(lineItem);
        RecalculateInvoiceTotals(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyDiscountAsync(Guid invoiceId, decimal discountTotal, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        invoice.DiscountTotal = discountTotal;
        RecalculateInvoiceTotals(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status != "Draft")
            throw new InvalidOperationException("Only draft invoices can be issued");

        invoice.Status = "Issued";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkInvoiceAsPaidAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status != "Issued")
            throw new InvalidOperationException("Only issued invoices can be marked as paid");

        invoice.Status = "Paid";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status == "Paid")
            throw new InvalidOperationException("Paid invoices cannot be cancelled");

        invoice.Status = "Cancelled";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLineQuantityAsync(Guid invoiceLineId, decimal quantity, CancellationToken cancellationToken = default)
    {
        var line = await _dbContext.InvoiceLines
            .FirstOrDefaultAsync(l => l.Id == invoiceLineId, cancellationToken);

        if (line == null)
            throw new InvalidOperationException($"Invoice line {invoiceLineId} not found");

        line.Quantity = quantity;
        line.LineTotal = quantity * line.UnitPrice;

        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == line.InvoiceId, cancellationToken);

        if (invoice != null)
        {
            RecalculateInvoiceTotals(invoice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLineUnitPriceAsync(Guid invoiceLineId, decimal unitPrice, CancellationToken cancellationToken = default)
    {
        var line = await _dbContext.InvoiceLines
            .FirstOrDefaultAsync(l => l.Id == invoiceLineId, cancellationToken);

        if (line == null)
            throw new InvalidOperationException($"Invoice line {invoiceLineId} not found");

        line.UnitPrice = unitPrice;
        line.LineTotal = line.Quantity * unitPrice;

        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == line.InvoiceId, cancellationToken);

        if (invoice != null)
        {
            RecalculateInvoiceTotals(invoice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void RecalculateInvoiceTotals(Invoice invoice)
    {
        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TaxTotal = invoice.Lines.Sum(x => x.LineTotal * x.TaxRate);
        invoice.Total = invoice.Subtotal + invoice.TaxTotal - invoice.DiscountTotal;
    }

    private static InvoiceResponse MapToResponse(Invoice invoice)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.CustomerAccountId,
            invoice.Id.ToString("N"),
            invoice.Currency,
            invoice.Total,
            Enum.Parse<Domain.Billing.InvoiceStatus>(invoice.Status),
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Lines.Select(li => new InvoiceLineItemResponse(
                li.Id,
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.LineTotal)).ToList());
    }
}
