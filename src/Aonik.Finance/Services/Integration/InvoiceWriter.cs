using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using Aonik.SharedKernel.Abstractions.Billing;

namespace Aonik.Finance.Services.Integration;

/// <summary>
/// Finance's implementation of the SharedKernel <see cref="IInvoiceWriter"/> write contract
/// (Spec 042 §12) — the write-side mirror of the ADR-006 read contracts. Lets modules that may not
/// reference Finance (e.g. <c>Aonik.Commerce</c>) raise an invoice for an order. A thin adapter over
/// <see cref="IBillingService"/>: <c>CustomerId</c> is the Finance <c>CustomerAccount</c> id.
/// </summary>
internal sealed class InvoiceWriter : IInvoiceWriter
{
    private readonly IBillingService _billing;

    public InvoiceWriter(IBillingService billing) => _billing = billing;

    public async Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken cancellationToken = default)
    {
        var request = new CreateInvoiceRequest(
            CustomerId: command.CustomerId,
            InvoiceNumber: GenerateInvoiceNumber(command.OrderId),
            Currency: command.Currency,
            DueUtc: command.DueUtc ?? DateTime.UtcNow.AddDays(7),
            LineItems: command.Lines
                .Select(l => new CreateInvoiceLineItemRequest(l.Description, l.Quantity, l.UnitPrice))
                .ToList());

        var response = await _billing.CreateInvoiceAsync(request, cancellationToken);
        return new InvoiceRef(response.Id, response.InvoiceNumber, response.TotalAmount, response.Currency);
    }

    private static string GenerateInvoiceNumber(Guid orderId)
        => $"INV-{orderId.ToString("N")[..8].ToUpperInvariant()}-{DateTime.UtcNow:yyMMddHHmmss}";
}
