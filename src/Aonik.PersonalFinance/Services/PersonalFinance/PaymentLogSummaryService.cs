using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

internal sealed class PaymentLogSummaryService : IPaymentLogSummaryService
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PaymentLogSummaryService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<YearSummary> GetYearSummaryAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var logs = _dbContext.PaymentLogs
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.Date.Year == year);

        var totals = await logs
            .GroupBy(p => p.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(p => p.Amount), g.Count()))
            .ToListAsync(cancellationToken);

        var entityCount = await logs
            .Select(p => p.CareEntityId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new YearSummary(year, totals.OrderBy(t => t.Currency).ToList(), entityCount);
    }
}
