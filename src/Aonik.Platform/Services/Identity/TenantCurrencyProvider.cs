using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Identity;

internal class TenantCurrencyProvider : ITenantCurrencyProvider
{
    private readonly PlatformDbContext _dbContext;

    public TenantCurrencyProvider(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<string>> GetTenantCurrencyCodesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantCurrencies = await _dbContext.TenantCurrencies
            .AsNoTracking()
            .Where(tc => tc.TenantId == tenantId)
            .Join(
                _dbContext.Currencies.AsNoTracking(),
                tc => tc.CurrencyId,
                c => c.Id,
                (_, currency) => currency.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        if (tenantCurrencies.Count > 0)
        {
            return tenantCurrencies;
        }

        var defaultCurrency = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.DefaultCurrency)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(defaultCurrency))
        {
            return new List<string> { defaultCurrency.Trim().ToUpperInvariant() };
        }

        return new List<string> { "USD" };
    }
}
