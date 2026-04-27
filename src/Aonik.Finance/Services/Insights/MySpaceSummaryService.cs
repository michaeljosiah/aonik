using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.Insights;
using Aonik.Finance.Contracts.Services.Insights;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Insights;

internal class MySpaceSummaryService : FinanceServiceBase, IMySpaceSummaryService
{
    private const int CashTimelineDays = 30;
    private const int CashProjectionDays = 30;
    private const int CashProjectionTrendWindow = 7;

    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAiRunStatsService _aiRunStats;
    private readonly IAgentProposalQueryService _agentProposals;
    private readonly ITenantCurrencyProvider _tenantCurrencyProvider;

    public MySpaceSummaryService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        IAiRunStatsService aiRunStats,
        IAgentProposalQueryService agentProposals,
        ITenantCurrencyProvider tenantCurrencyProvider)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _aiRunStats = aiRunStats;
        _agentProposals = agentProposals;
        _tenantCurrencyProvider = tenantCurrencyProvider;
    }

    public async Task<MySpaceSummaryResponse> GetSummaryAsync(
        string? currencyOverride = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Cross-module reads — different DbContexts, safe to run in parallel.
        var agentOpsTask = _aiRunStats.CountForTodayAsync(cancellationToken);
        var proposalsTask = _agentProposals.ListPendingAsync(5, cancellationToken);
        var currencyTask = _tenantCurrencyProvider.GetTenantCurrencyCodesAsync(tenantId, cancellationToken);

        // Resolve the timeline currency before the finance reads so the daily
        // series can filter by it.
        var currencyCodes = await currencyTask;
        var primaryCurrency = currencyCodes.Count > 0 ? currencyCodes[0] : "USD";
        var timelineCurrency = ResolveTimelineCurrency(currencyOverride, currencyCodes, primaryCurrency);

        // Finance-context reads share a single DbContext — must run serially.
        var metrics = await BuildFinancialMetricsAsync(tenantId, cancellationToken);
        var activity = await BuildActivityFeedAsync(tenantId, cancellationToken);
        var historical = await BuildDailyCashSeriesAsync(tenantId, timelineCurrency, cancellationToken);
        var cashUpdatedAt = await GetCashPositionUpdatedAtAsync(tenantId, cancellationToken);
        var (projected, projectedLow, projectedLowAt) = BuildProjection(historical, CashProjectionDays);
        var revenueEvents = await BuildRevenueEventsAsync(
            tenantId, timelineCurrency, CashProjectionDays, cancellationToken);

        await Task.WhenAll(agentOpsTask, proposalsTask);
        var agentOpsToday = agentOpsTask.Result;
        var proposalSummaries = proposalsTask.Result;

        var availableCurrencies = currencyCodes.Count > 0
            ? (IReadOnlyList<string>)currencyCodes
            : new[] { "USD" };

        var apiProposals = proposalSummaries
            .Select(s => new AgentProposalDto(
                s.Id,
                s.AgentName,
                s.AgentDomain,
                s.AgentIconUrl,
                s.Confidence,
                s.Summary,
                s.Reason,
                s.RiskTier,
                s.CreatedAt))
            .ToList();

        var cashTimeline = new CashTimelineDto(
            Currency: timelineCurrency,
            AvailableCurrencies: availableCurrencies,
            Historical: historical,
            Projected: projected,
            Events: revenueEvents,
            ProjectedLow: projectedLow,
            ProjectedLowAt: projectedLowAt);

        return new MySpaceSummaryResponse(
            metrics,
            activity,
            agentOpsToday,
            cashUpdatedAt,
            cashTimeline,
            apiProposals);
    }

    /// <summary>
    /// Picks the timeline currency: caller override if it's in the tenant's
    /// configured list, otherwise the tenant primary, with USD as the final
    /// fallback. Case-insensitive — codes are normalised to upper-case.
    /// </summary>
    private static string ResolveTimelineCurrency(
        string? requested,
        IReadOnlyList<string> available,
        string primary)
    {
        if (string.IsNullOrWhiteSpace(requested)) return primary;

        var normalized = requested.Trim().ToUpperInvariant();
        foreach (var code in available)
        {
            if (string.Equals(code, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }
        return primary;
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

    /// <summary>
    /// Builds a 30-day daily running cash balance for the current tenant in
    /// the supplied <paramref name="currency"/>, by projecting asset-account
    /// journal entry lines (filtered to that currency) into per-day deltas
    /// applied to a baseline computed from all matching entries before the
    /// window. The series always returns exactly <c>CashTimelineDays</c>
    /// points covering today and the previous 29 days inclusive — days with
    /// no activity carry the previous balance forward.
    /// </summary>
    private async Task<IReadOnlyList<CashTimelinePointDto>> BuildDailyCashSeriesAsync(
        Guid tenantId, string currency, CancellationToken ct)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var windowStart = todayUtc.AddDays(-(CashTimelineDays - 1));
        var windowEndExclusive = todayUtc.AddDays(1);

        var assetAccountIds = await _dbContext.LedgerAccounts
            .Where(a => a.TenantId == tenantId && a.AccountType == "Asset")
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (assetAccountIds.Count == 0)
        {
            return BuildFlatSeries(windowStart, 0m);
        }

        var assetSet = assetAccountIds.ToHashSet();

        // Baseline = sum of asset-side debits/credits in this currency before the window.
        var baselineLines = await _dbContext.JournalEntryLines
            .Where(l => l.TenantId == tenantId
                && assetSet.Contains(l.LedgerAccountId)
                && l.Currency == currency)
            .Join(
                _dbContext.JournalEntries.Where(e => e.TenantId == tenantId && e.Timestamp < windowStart),
                l => l.JournalEntryId,
                e => e.Id,
                (l, _) => new { l.Direction, l.Amount })
            .ToListAsync(ct);

        var baseline = baselineLines.Sum(l =>
            l.Direction.Equals("Debit", StringComparison.OrdinalIgnoreCase) ? l.Amount : -l.Amount);

        // Daily deltas across the window, in the requested currency only.
        var windowLines = await _dbContext.JournalEntryLines
            .Where(l => l.TenantId == tenantId
                && assetSet.Contains(l.LedgerAccountId)
                && l.Currency == currency)
            .Join(
                _dbContext.JournalEntries.Where(e =>
                    e.TenantId == tenantId &&
                    e.Timestamp >= windowStart &&
                    e.Timestamp < windowEndExclusive),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) => new { l.Direction, l.Amount, EntryDate = e.Timestamp.Date })
            .ToListAsync(ct);

        var deltaByDate = windowLines
            .GroupBy(l => l.EntryDate)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(l => l.Direction.Equals("Debit", StringComparison.OrdinalIgnoreCase)
                    ? l.Amount
                    : -l.Amount));

        var series = new List<CashTimelinePointDto>(CashTimelineDays);
        var running = baseline;
        for (var i = 0; i < CashTimelineDays; i++)
        {
            var day = windowStart.AddDays(i);
            if (deltaByDate.TryGetValue(day, out var delta))
            {
                running += delta;
            }
            series.Add(new CashTimelinePointDto(day, running));
        }
        return series;
    }

    private static IReadOnlyList<CashTimelinePointDto> BuildFlatSeries(DateTime windowStart, decimal value)
    {
        var series = new List<CashTimelinePointDto>(CashTimelineDays);
        for (var i = 0; i < CashTimelineDays; i++)
        {
            series.Add(new CashTimelinePointDto(windowStart.AddDays(i), value));
        }
        return series;
    }

    /// <summary>
    /// Returns the most recent journal-entry timestamp for the tenant — used
    /// as the "cash position updated …" freshness signal in the dashboard
    /// header. Null when the tenant has no entries yet.
    /// </summary>
    private async Task<DateTime?> GetCashPositionUpdatedAtAsync(Guid tenantId, CancellationToken ct)
    {
        return await _dbContext.JournalEntries
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => (DateTime?)e.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Naive forward projection — extrapolates a 7-day moving-average daily
    /// delta from the tail of the historical series for <paramref name="forwardDays"/>
    /// days. Cheap and useful for v1 dashboards; replaced by a real
    /// scheduled-entry projection when those domain primitives land.
    /// ProjectedLow is the minimum balance across "now and forward" — i.e.
    /// the last historical point and every projected point.
    /// </summary>
    private static (IReadOnlyList<CashTimelinePointDto> Projected, decimal? ProjectedLow, DateTime? ProjectedLowAt)
        BuildProjection(IReadOnlyList<CashTimelinePointDto> historical, int forwardDays)
    {
        if (historical.Count == 0)
        {
            return (Array.Empty<CashTimelinePointDto>(), null, null);
        }

        // Per-day average delta over the trend window (capped at the available history).
        var window = Math.Min(CashProjectionTrendWindow, historical.Count);
        decimal averageDelta = 0m;
        if (window >= 2)
        {
            var startIndex = historical.Count - window;
            var spread = historical[^1].Balance - historical[startIndex].Balance;
            averageDelta = spread / (window - 1);
        }

        var lastDate = historical[^1].Date;
        var lastBalance = historical[^1].Balance;

        var projected = new List<CashTimelinePointDto>(forwardDays);
        var running = lastBalance;
        for (var i = 1; i <= forwardDays; i++)
        {
            running += averageDelta;
            projected.Add(new CashTimelinePointDto(lastDate.AddDays(i), running));
        }

        // Compute the low across "from here on" (last historical + projected).
        var low = lastBalance;
        var lowAt = lastDate;
        foreach (var point in projected)
        {
            if (point.Balance < low)
            {
                low = point.Balance;
                lowAt = point.Date;
            }
        }
        return (projected, low, lowAt);
    }

    /// <summary>
    /// Builds revenue event markers from invoices in the requested currency
    /// whose <c>DueDate</c> falls inside the projection window. The
    /// projection itself is currency-naive (it just trends the historical
    /// series); the markers are a separate signal — when revenue is
    /// expected to land — so the dashboard can show context dots even
    /// though the projection line is naive.
    /// </summary>
    private async Task<IReadOnlyList<CashTimelineEventDto>> BuildRevenueEventsAsync(
        Guid tenantId, string currency, int forwardDays, CancellationToken ct)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var windowEnd = todayUtc.AddDays(forwardDays);

        var invoices = await _dbContext.Invoices
            .Where(i => i.TenantId == tenantId
                && i.Status == "Issued"
                && i.Currency == currency
                && i.DueDate >= todayUtc
                && i.DueDate <= windowEnd)
            .OrderBy(i => i.DueDate)
            .Select(i => new { i.DueDate, i.Total })
            .ToListAsync(ct);

        return invoices
            .Select(i => new CashTimelineEventDto(
                Date: i.DueDate.Date,
                Kind: "revenue",
                Label: $"Invoice due · {i.Total:N0}",
                Amount: i.Total))
            .ToList();
    }
}
