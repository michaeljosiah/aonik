using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Finance;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// FinanceDbContext-backed implementation of <see cref="ICustomerInvoiceHistoryReader"/>.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class CustomerInvoiceHistoryReader : ICustomerInvoiceHistoryReader
{
    private readonly FinanceDbContext _dbContext;

    public CustomerInvoiceHistoryReader(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InvoiceHistoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> invoiceIds,
        CancellationToken cancellationToken = default)
    {
        if (invoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && invoiceIds.Contains(i.Id))
            .Select(i => new InvoiceHistoryItem(
                i.Id,
                i.OrderId,
                i.Status,
                i.Currency,
                i.Total,
                i.IssueDate,
                i.DueDate))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Invoices
            .AsNoTracking()
            .AnyAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken);
    }
}
