using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Billing;
using Aonik.Domain.Billing.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Billing;

public class BillingService : IBillingService
{
    private readonly IAonikDbContext _dbContext;

    public BillingService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = new Invoice(
            request.CustomerId,
            request.InvoiceNumber,
            request.Currency,
            request.DueUtc);

        foreach (var lineItemRequest in request.LineItems)
        {
            var lineItem = new InvoiceLineItem(
                invoice.Id,
                lineItemRequest.Description,
                lineItemRequest.Quantity,
                lineItemRequest.UnitPrice);

            invoice.AddLineItem(lineItem);
        }

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        return invoice == null ? null : MapToResponse(invoice);
    }

    private static InvoiceResponse MapToResponse(Invoice invoice)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.CustomerId,
            invoice.InvoiceNumber,
            invoice.Currency,
            invoice.TotalAmount,
            invoice.Status,
            invoice.IssuedUtc,
            invoice.DueUtc,
            invoice.LineItems.Select(li => new InvoiceLineItemResponse(
                li.Id,
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.LineTotal)).ToList());
    }
}
