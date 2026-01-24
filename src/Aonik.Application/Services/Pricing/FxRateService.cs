using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Pricing;

public class FxRateService : IFxRateService
{
    private readonly IAonikDbContext _dbContext;
    private readonly IClock _clock;

    public FxRateService(IAonikDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<FxRateResult> GetRateAsync(
        string baseCurrency,
        string targetCurrency,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var fxQuote = await _dbContext.FxQuotes
            .Where(rate => rate.BaseCurrency == baseCurrency)
            .Where(rate => rate.TargetCurrency == targetCurrency)
            .Where(rate => rate.ExpiresAt >= now)
            .OrderByDescending(rate => rate.ExpiresAt)
            .ThenByDescending(rate => rate.UpdatedAt ?? rate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (fxQuote == null)
        {
            throw new InvalidOperationException("FX rate not available for the requested currency pair.");
        }

        var timestamp = fxQuote.UpdatedAt ?? fxQuote.CreatedAt;
        if (timestamp == default)
        {
            timestamp = now;
        }

        var timestampUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        return new FxRateResult(
            fxQuote.Id,
            fxQuote.Rate,
            new DateTimeOffset(timestampUtc),
            fxQuote.Provider);
    }
}
