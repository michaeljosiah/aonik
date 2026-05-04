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
}
