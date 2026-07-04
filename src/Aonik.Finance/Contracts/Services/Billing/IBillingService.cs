using Aonik.Finance.Contracts.Models.Billing;

namespace Aonik.Finance.Contracts.Services.Billing;

public interface IBillingService
{
    Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task AddLineToInvoiceAsync(Guid invoiceId, CreateInvoiceLineItemRequest lineRequest, CancellationToken cancellationToken = default);
    Task ApplyDiscountAsync(Guid invoiceId, decimal discountTotal, CancellationToken cancellationToken = default);
    Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task MarkInvoiceAsPaidAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task CancelInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task UpdateLineQuantityAsync(Guid invoiceLineId, decimal quantity, CancellationToken cancellationToken = default);
    Task UpdateLineUnitPriceAsync(Guid invoiceLineId, decimal unitPrice, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceResponse>> ListInvoicesAsync(
        string? statusFilter = null,
        int pageNumber = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default);
}
