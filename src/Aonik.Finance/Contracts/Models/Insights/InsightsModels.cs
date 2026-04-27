namespace Aonik.Finance.Contracts.Models.Insights;

public record MySpaceSummaryResponse(
    IReadOnlyList<FinancialMetricDto> FinancialMetrics,
    IReadOnlyList<ActivityItemDto> RecentActivity,
    int AgentOpsToday,
    DateTime? CashPositionUpdatedAt,
    CashTimelineDto CashTimeline,
    IReadOnlyList<AgentProposalDto> AgentProposals);

public record FinancialMetricDto(
    string MetricKey,
    string FormattedValue,
    string? ValueLabel,
    string TrendDirection,
    decimal TrendPercent,
    decimal[] Sparkline,
    int? Count,
    decimal? Total);

public record ActivityItemDto(
    string Id,
    string Title,
    string? Description,
    string Timestamp,
    string Icon);

/// <summary>
/// Daily cash position series for the dashboard's Cash Timeline chart.
/// Currency is the one the series is computed in; AvailableCurrencies is the
/// tenant's configured currency set so the switcher can offer alternates.
/// Wave 4b shipped historical only; Projected / Events / ProjectedLow are
/// populated by Wave 4c.4 (cash projection + event markers).
/// </summary>
public record CashTimelineDto(
    string Currency,
    IReadOnlyList<string> AvailableCurrencies,
    IReadOnlyList<CashTimelinePointDto> Historical,
    IReadOnlyList<CashTimelinePointDto> Projected,
    IReadOnlyList<CashTimelineEventDto> Events,
    decimal? ProjectedLow,
    DateTime? ProjectedLowAt);

public record CashTimelinePointDto(DateTime Date, decimal Balance);

/// <summary>Marker on the timeline (revenue event, payroll, payout, etc.).</summary>
public record CashTimelineEventDto(DateTime Date, string Kind, string Label, decimal Amount);

/// <summary>
/// Pending agent proposal projected for the dashboard. Mirrors
/// <see cref="Aonik.SharedKernel.Abstractions.Agents.AgentProposalSummary"/>
/// at the API contract boundary so the public DTO doesn't depend on the
/// SharedKernel namespace.
/// </summary>
public record AgentProposalDto(
    Guid Id,
    string AgentName,
    string AgentDomain,
    string? AgentIconUrl,
    decimal Confidence,
    string Summary,
    string? Reason,
    string RiskTier,
    DateTime CreatedAt);
