using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Persistence;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Pricing;

internal class FxRateService : IFxRateService
{
    private readonly FinanceDbContext _dbContext;
    private readonly IClock _clock;

    public FxRateService(FinanceDbContext dbContext, IClock clock)
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

    public async Task<FxRateHistoryResult> GetRateHistoryAsync(
        string baseCurrency,
        string targetCurrency,
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var cutoff = now.AddDays(-days);

        // Fetch all quotes for the pair within the requested window.
        var quotes = await _dbContext.FxQuotes
            .Where(q => q.BaseCurrency == baseCurrency && q.TargetCurrency == targetCurrency)
            .Where(q => q.CreatedAt >= cutoff)
            .OrderBy(q => q.CreatedAt)
            .Select(q => new { q.CreatedAt, q.Rate })
            .ToListAsync(cancellationToken);

        // Group by date and take the latest rate per day.
        var ratePoints = quotes
            .GroupBy(q => q.CreatedAt.Date)
            .Select(g => new FxRatePoint(
                g.Key.ToString("MMM dd"),
                g.OrderByDescending(q => q.CreatedAt).First().Rate))
            .ToList();

        // Compute a simple buy/hold/wait signal.
        string signal;
        string signalReason;

        if (ratePoints.Count < 2)
        {
            signal = "hold";
            signalReason = "Not enough historical data to determine a trend.";
        }
        else
        {
            var average = ratePoints.Average(r => r.Rate);
            var current = ratePoints[^1].Rate;
            var percentDiff = (current - average) / average * 100m;

            if (percentDiff > 1m)
            {
                signal = "buy";
                signalReason = $"Rate is {percentDiff:F1}% above the {days}-day average and trending favourably. Sending now locks in a good window.";
            }
            else if (percentDiff < -1m)
            {
                signal = "wait";
                signalReason = $"Rate is {Math.Abs(percentDiff):F1}% below the {days}-day average. Consider waiting for a better window.";
            }
            else
            {
                signal = "hold";
                signalReason = $"Rate is within 1% of the {days}-day average. No strong signal either way.";
            }
        }

        return new FxRateHistoryResult(
            baseCurrency,
            targetCurrency,
            ratePoints,
            signal,
            signalReason);
    }
}
