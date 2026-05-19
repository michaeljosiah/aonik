using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Finance;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// FinanceDbContext-backed implementation of <see cref="IFxQuoteReader"/>.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class FxQuoteReader : IFxQuoteReader
{
    private readonly FinanceDbContext _dbContext;

    public FxQuoteReader(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FxQuoteHistoryItem>> GetRecentForCurrenciesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> currencies,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (currencies.Count < 2 || limit <= 0)
        {
            return [];
        }

        return await _dbContext.FxQuotes
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId
                && q.BaseCurrency != q.TargetCurrency
                && currencies.Contains(q.BaseCurrency)
                && currencies.Contains(q.TargetCurrency))
            .OrderByDescending(q => q.ExpiresAt)
            .ThenByDescending(q => q.UpdatedAt ?? q.CreatedAt)
            .Take(limit)
            .Select(q => new FxQuoteHistoryItem(
                q.Id,
                q.BaseCurrency,
                q.TargetCurrency,
                q.Rate,
                q.ExpiresAt,
                q.UpdatedAt ?? q.CreatedAt,
                q.Provider))
            .ToListAsync(cancellationToken);
    }
}
