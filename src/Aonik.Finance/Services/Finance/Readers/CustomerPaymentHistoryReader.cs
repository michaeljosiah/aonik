using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Finance;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// FinanceDbContext-backed implementation of <see cref="ICustomerPaymentHistoryReader"/>.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class CustomerPaymentHistoryReader : ICustomerPaymentHistoryReader
{
    private readonly FinanceDbContext _dbContext;

    public CustomerPaymentHistoryReader(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PaymentHistoryItem>> GetForOrderOrInvoiceAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> orderIds,
        IReadOnlyCollection<Guid> invoiceIds,
        CancellationToken cancellationToken = default)
    {
        if (orderIds.Count == 0 && invoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && (orderIds.Contains(p.OrderId)
                    || (p.InvoiceId.HasValue && invoiceIds.Contains(p.InvoiceId.Value))))
            .Select(p => new PaymentHistoryItem(
                p.Id,
                p.OrderId,
                p.InvoiceId,
                p.Status,
                p.Amount,
                p.Currency,
                p.PurposeType,
                p.PurposeId))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentIntents
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == paymentIntentId, cancellationToken);
    }
}
