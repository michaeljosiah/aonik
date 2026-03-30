using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.Insights;
using Aonik.Finance.Contracts.Services.Insights;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Insights;

internal class MySpaceSummaryService : FinanceServiceBase, IMySpaceSummaryService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public MySpaceSummaryService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<MySpaceSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var metrics = await BuildFinancialMetricsAsync(tenantId, cancellationToken);
        var activity = await BuildActivityFeedAsync(tenantId, cancellationToken);

        return new MySpaceSummaryResponse(metrics, activity);
    }

    private async Task<IReadOnlyList<FinancialMetricDto>> BuildFinancialMetricsAsync(
        Guid tenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowStart = currentMonthStart.AddMonths(-5);

        // Build account type lookup
        var accounts = await _dbContext.LedgerAccounts
            .Where(a => a.TenantId == tenantId)
            .Select(a => new { a.Id, a.AccountType })
            .ToListAsync(ct);

        var accountTypeMap = accounts.ToDictionary(a => a.Id, a => a.AccountType);

        // Query journal entry lines within the 6-month window
        var lines = await _dbContext.JournalEntryLines
            .Where(l => l.TenantId == tenantId)
            .Join(
                _dbContext.JournalEntries.Where(e => e.TenantId == tenantId && e.Timestamp >= windowStart),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) => new { l.LedgerAccountId, l.Direction, l.Amount, EntryMonth = e.Timestamp })
            .ToListAsync(ct);

        // Baseline for cash position: all asset entries before the window
        var assetAccountIds = accountTypeMap
            .Where(kv => kv.Value.Equals("Asset", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToHashSet();

        var cashBaseline = await _dbContext.JournalEntryLines
            .Where(l => l.TenantId == tenantId && assetAccountIds.Contains(l.LedgerAccountId))
            .Join(
                _dbContext.JournalEntries.Where(e => e.TenantId == tenantId && e.Timestamp < windowStart),
                l => l.JournalEntryId,
                e => e.Id,
                (l, _) => new { l.Direction, l.Amount })
            .ToListAsync(ct);

        var baselineCash = cashBaseline.Sum(l =>
            l.Direction.Equals("Debit", StringComparison.OrdinalIgnoreCase) ? l.Amount : -l.Amount);

        // Group lines by month and compute per-month metrics
        var monthlyRevenue = new decimal[6];
        var monthlyExpenses = new decimal[6];
        var monthlyCashDelta = new decimal[6];

        foreach (var line in lines)
        {
            var monthStart = new DateTime(line.EntryMonth.Year, line.EntryMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthIndex = (int)((monthStart.Year - windowStart.Year) * 12 + monthStart.Month - windowStart.Month);
            if (monthIndex < 0 || monthIndex >= 6) continue;

            if (!accountTypeMap.TryGetValue(line.LedgerAccountId, out var accountType)) continue;

            var isDebit = line.Direction.Equals("Debit", StringComparison.OrdinalIgnoreCase);

            if (accountType.Equals("Revenue", StringComparison.OrdinalIgnoreCase))
            {
                monthlyRevenue[monthIndex] += isDebit ? -line.Amount : line.Amount;
            }
            else if (accountType.Equals("Expense", StringComparison.OrdinalIgnoreCase))
            {
                monthlyExpenses[monthIndex] += isDebit ? line.Amount : -line.Amount;
            }

            if (assetAccountIds.Contains(line.LedgerAccountId))
            {
                monthlyCashDelta[monthIndex] += isDebit ? line.Amount : -line.Amount;
            }
        }

        // Build cumulative cash position sparkline
        var cashSparkline = new decimal[6];
        var runningCash = baselineCash;
        for (var i = 0; i < 6; i++)
        {
            runningCash += monthlyCashDelta[i];
            cashSparkline[i] = runningCash;
        }

        // P&L = Revenue - Expenses per month
        var plSparkline = new decimal[6];
        for (var i = 0; i < 6; i++)
            plSparkline[i] = monthlyRevenue[i] - monthlyExpenses[i];

        // Burn rate = average monthly expenses
        var burnRate = monthlyExpenses.Average();

        // Outstanding invoices
        var outstandingInvoices = await _dbContext.Invoices
            .Where(i => i.TenantId == tenantId && i.Status == "Issued")
            .Select(i => new { i.Total })
            .ToListAsync(ct);

        var outstandingCount = outstandingInvoices.Count;
        var outstandingTotal = outstandingInvoices.Sum(i => i.Total);

        // Current month index = 5 (last element)
        var currentRevenue = monthlyRevenue[5];
        var currentExpenses = monthlyExpenses[5];
        var currentCash = cashSparkline[5];
        var currentPl = plSparkline[5];

        var metrics = new List<FinancialMetricDto>
        {
            BuildMetric("burn-rate", burnRate, $"${burnRate:N0}/mo",
                ComputeRunway(currentCash, burnRate),
                monthlyExpenses),

            BuildMetric("revenue", currentRevenue, $"${currentRevenue:N0}",
                "This month",
                monthlyRevenue),

            BuildMetric("outstanding-invoices", outstandingCount,
                $"{outstandingCount} unpaid",
                $"${outstandingTotal:N0} outstanding",
                Array.Empty<decimal>(),
                outstandingCount, outstandingTotal),

            BuildMetric("expenses", currentExpenses, $"${currentExpenses:N0}",
                "This month",
                monthlyExpenses),

            BuildMetric("cash-position", currentCash, $"${currentCash:N0}",
                $"Net this month: ${monthlyCashDelta[5]:N0}",
                cashSparkline),

            BuildMetric("profit-loss", currentPl, $"${currentPl:N0}",
                currentRevenue > 0 ? $"Net margin: {(currentPl / currentRevenue * 100):N0}%" : null,
                plSparkline),
        };

        return metrics;
    }

    private static FinancialMetricDto BuildMetric(
        string key, decimal currentValue, string formattedValue, string? valueLabel,
        decimal[] sparkline, int? count = null, decimal? total = null)
    {
        var (direction, percent) = sparkline.Length >= 2
            ? ComputeTrend(sparkline[^1], sparkline[^2])
            : ("neutral", 0m);

        return new FinancialMetricDto(key, formattedValue, valueLabel, direction, percent, sparkline, count, total);
    }

    private static (string Direction, decimal Percent) ComputeTrend(decimal current, decimal previous)
    {
        if (previous == 0)
            return current == 0 ? ("neutral", 0) : ("up", 100);

        var change = (current - previous) / Math.Abs(previous) * 100;
        var direction = change > 0 ? "up" : change < 0 ? "down" : "neutral";
        return (direction, Math.Abs(Math.Round(change, 1)));
    }

    private static string? ComputeRunway(decimal cash, decimal burnRate)
    {
        if (burnRate <= 0) return null;
        var months = cash / burnRate;
        return $"Runway: {months:N1} months at current rate";
    }

    private async Task<IReadOnlyList<ActivityItemDto>> BuildActivityFeedAsync(
        Guid tenantId, CancellationToken ct)
    {
        var events = await _dbContext.Set<Aonik.Finance.Entities.Orders.OrderHistoryEvent>()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.EventAt)
            .Take(10)
            .Select(e => new { e.Id, e.EventType, e.EventAt, e.OrderId })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        return events.Select(e => new ActivityItemDto(
            e.Id.ToString(),
            FormatEventTitle(e.EventType),
            $"Order {e.OrderId.ToString()[..8]}",
            FormatRelativeTime(e.EventAt, now),
            MapEventIcon(e.EventType)
        )).ToList();
    }

    private static string FormatEventTitle(string eventType) => eventType switch
    {
        "OrderCreated" => "New order created",
        "OrderSubmitted" => "Order submitted for processing",
        "OrderCompleted" => "Order completed successfully",
        "OrderCancelled" => "Order cancelled",
        "OrderFunded" => "Order funding received",
        "PaymentCaptured" => "Payment captured",
        "PaymentFailed" => "Payment failed",
        "ItemAdded" => "Item added to order",
        "ItemRemoved" => "Item removed from order",
        _ => eventType
    };

    private static string MapEventIcon(string eventType) => eventType switch
    {
        "OrderCreated" => "FileText",
        "OrderSubmitted" => "Send",
        "OrderCompleted" => "CheckCircle",
        "OrderCancelled" => "XCircle",
        "OrderFunded" => "DollarSign",
        "PaymentCaptured" => "CheckCircle",
        "PaymentFailed" => "AlertCircle",
        _ => "Activity"
    };

    private static string FormatRelativeTime(DateTime eventAt, DateTime now)
    {
        var diff = now - eventAt;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d ago";
        return eventAt.ToString("MMM dd");
    }
}
