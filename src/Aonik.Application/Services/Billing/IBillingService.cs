using Aonik.Application.Models.Billing;

namespace Aonik.Application.Services.Billing;

public interface IBillingService
{
    Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
