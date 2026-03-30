namespace Aonik.Finance.Contracts.Models.Insights;

public record MySpaceSummaryResponse(
    IReadOnlyList<FinancialMetricDto> FinancialMetrics,
    IReadOnlyList<ActivityItemDto> RecentActivity);

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
