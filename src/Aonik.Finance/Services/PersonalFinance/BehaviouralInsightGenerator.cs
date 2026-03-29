using System.Text.Json;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Pre-computes expensive behavioural insights from historical transaction data
/// and writes them to the Insights table with SubjectType = "UserBehaviour".
/// Called by background jobs — not invoked on-demand.
/// </summary>
internal sealed class BehaviouralInsightGenerator
{
    private const string SubjectType = "UserBehaviour";
    private const int MinMonthsForPatterns = 3;

    private readonly FinanceDbContext _dbContext;
    private readonly IInsightWriter _insightWriter;
    private readonly ILogger<BehaviouralInsightGenerator> _logger;

    public BehaviouralInsightGenerator(
        FinanceDbContext dbContext,
        IInsightWriter insightWriter,
        ILogger<BehaviouralInsightGenerator> logger)
    {
        _dbContext = dbContext;
        _insightWriter = insightWriter;
        _logger = logger;
    }

    /// <summary>
    /// Runs all insight generators for a specific user.
    /// </summary>
    public async Task GenerateAllForUserAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        await DetectLateMonthSpendingAsync(tenantId, userId, cancellationToken);
        await DetectIncomeRhythmAsync(tenantId, userId, cancellationToken);
        await DetectRecurringMerchantsAsync(tenantId, userId, cancellationToken);
    }

    /// <summary>
    /// Detects if the user tends to spend more in the last week of each month.
    /// Requires 3+ months of transaction history.
    /// </summary>
    private async Task DetectLateMonthSpendingAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-MinMonthsForPatterns);

        var transactions = await _dbContext.PersonalTransactions
            .Where(t => t.TenantId == tenantId && t.UserId == userId
                && t.OccurredAt >= cutoff && t.Amount < 0)
            .Select(t => new { t.OccurredAt, t.Amount })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (transactions.Count < 30) return; // Not enough data

        var lateMonthSpend = transactions
            .Where(t => t.OccurredAt.Day >= 25)
            .Sum(t => Math.Abs(t.Amount));

        var earlyMonthSpend = transactions
            .Where(t => t.OccurredAt.Day < 25)
            .Sum(t => Math.Abs(t.Amount));

        var earlyDays = 24.0m;
        var lateDays = 6.0m;
        var lateMonthDailyAvg = lateDays > 0 ? lateMonthSpend / lateDays : 0;
        var earlyMonthDailyAvg = earlyDays > 0 ? earlyMonthSpend / earlyDays : 0;

        if (earlyMonthDailyAvg > 0 && lateMonthDailyAvg / earlyMonthDailyAvg > 1.2m)
        {
            var percentIncrease = Math.Round((lateMonthDailyAvg / earlyMonthDailyAvg - 1) * 100, 0);
            var metadata = JsonSerializer.Serialize(new
            {
                confidence = 0.8,
                percentIncrease,
                observedMonths = MinMonthsForPatterns
            });

            await _insightWriter.SaveInsightAsync(
                SubjectType,
                userId,
                "Late-month spending spike",
                $"You tend to spend {percentIncrease}% more per day in the last week of each month.",
                cancellationToken);
        }
    }

    /// <summary>
    /// Detects the user's income rhythm (which day of month income typically arrives).
    /// </summary>
    private async Task DetectIncomeRhythmAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-MinMonthsForPatterns);

        var incomeTransactions = await _dbContext.PersonalTransactions
            .Where(t => t.TenantId == tenantId && t.UserId == userId
                && t.OccurredAt >= cutoff && t.Amount > 0
                && (t.Category == "income" || t.Category == "salary"))
            .Select(t => new { t.OccurredAt, t.Amount })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (incomeTransactions.Count < MinMonthsForPatterns) return;

        var dayGroups = incomeTransactions
            .GroupBy(t => t.OccurredAt.Day)
            .OrderByDescending(g => g.Count())
            .First();

        var payday = dayGroups.Key;
        var variance = incomeTransactions.Select(t => Math.Abs(t.OccurredAt.Day - payday)).Average();

        await _insightWriter.SaveInsightAsync(
            SubjectType,
            userId,
            "Income rhythm detected",
            $"Income typically arrives around the {payday}{GetDaySuffix(payday)} of each month (±{Math.Round(variance, 0)} days).",
            cancellationToken);
    }

    /// <summary>
    /// Detects merchants that appear in 2+ consecutive months (potential subscriptions).
    /// </summary>
    private async Task DetectRecurringMerchantsAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-3);

        var merchantData = await _dbContext.PersonalTransactions
            .Where(t => t.TenantId == tenantId && t.UserId == userId
                && t.OccurredAt >= cutoff && t.Amount < 0
                && t.Merchant != null && t.Merchant != "")
            .Select(t => new { t.Merchant, Month = t.OccurredAt.Month, t.OccurredAt.Year })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var recurring = merchantData
            .GroupBy(t => t.Merchant)
            .Where(g => g.Select(t => $"{t.Year}-{t.Month}").Distinct().Count() >= 2)
            .Select(g => g.Key!)
            .ToList();

        // Filter out merchants already tracked as subscriptions
        var existingSubscriptions = await _dbContext.Subscriptions
            .Where(s => s.TenantId == tenantId && s.UserId == userId && s.Status == "active")
            .Select(s => s.Merchant)
            .ToListAsync(cancellationToken);

        var newRecurring = recurring.Except(existingSubscriptions, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var merchant in newRecurring.Take(5))
        {
            await _insightWriter.SaveInsightAsync(
                SubjectType,
                userId,
                "Potential recurring payment detected",
                $"You've made payments to {merchant} in multiple consecutive months. This may be a subscription.",
                cancellationToken);
        }
    }

    private static string GetDaySuffix(int day) => day switch
    {
        1 or 21 or 31 => "st",
        2 or 22 => "nd",
        3 or 23 => "rd",
        _ => "th"
    };
}
