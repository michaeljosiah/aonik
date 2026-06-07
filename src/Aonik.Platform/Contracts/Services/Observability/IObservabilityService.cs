using Aonik.Platform.Contracts.Api.Observability;

namespace Aonik.Platform.Contracts.Services.Observability;

public interface IObservabilityService
{
    Task<ObservabilityOverviewResponse> GetOverviewAsync(string timeRange, CancellationToken cancellationToken = default);
    Task<ErrorsResponse> GetErrorsAsync(string timeRange, string? operationId = null, CancellationToken cancellationToken = default);
    Task<ErrorDetailResponse> GetErrorDetailAsync(string problemId, string timeRange, CancellationToken cancellationToken = default);
    Task<DependencyMetricsResponse> GetDependenciesAsync(string timeRange, CancellationToken cancellationToken = default);
    Task<AiMetricsResponse> GetAiMetricsAsync(string timeRange, CancellationToken cancellationToken = default);
    Task<JobMetricsResponse> GetJobMetricsAsync(string timeRange, CancellationToken cancellationToken = default);
    Task<StructuredLogsResponse> GetStructuredLogsAsync(string timeRange, string? severity = null, CancellationToken cancellationToken = default);
    Task<AiPerformanceResponse> GetAiPerformanceAsync(string timeRange, CancellationToken cancellationToken = default);
    Task<RetrievalResponse> GetRetrievalAsync(string timeRange, CancellationToken cancellationToken = default);
    Task<TopologyResponse> GetTopologyAsync(string timeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every observable signal tied to the given OrderId across the
    /// money-action lifecycle (Quote / Confirm / Capture / Transmit / Settle /
    /// Webhook). Backs GitHub Issue #142 — operators paste an OrderId and
    /// receive the full trace, with end-to-end wall-clock measured against
    /// the 30s SLA. The Quote stage is chained via PricingQuoteId (resolved
    /// from the Confirm-stage log) because quote logs precede the order.
    /// </summary>
    Task<MoneyActionTraceResponse> GetMoneyActionTraceAsync(Guid orderId, string timeRange, CancellationToken cancellationToken = default);
}
