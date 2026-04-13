namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Contract for persisting AI run execution metadata.
/// Implemented by the AI module and consumed by domain workflows.
/// </summary>
public interface IAiRunWriter
{
    Task<Guid> StartRunAsync(
        string useCase,
        string inputRefsJson,
        CancellationToken cancellationToken = default);

    Task MarkRunCompletedAsync(
        Guid aiRunId,
        string? outputRef = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the run as completed and persists token usage, latency, and cost metrics.
    /// If <paramref name="costEstimate"/> is zero the implementation may auto-compute
    /// cost from the model's <c>CostProfileJson</c>.
    /// </summary>
    Task MarkRunCompletedWithMetricsAsync(
        Guid aiRunId,
        int tokensUsed,
        int latencyMs,
        decimal costEstimate,
        string? outputRef = null,
        CancellationToken cancellationToken = default);

    Task MarkRunFailedAsync(
        Guid aiRunId,
        string failureReason,
        CancellationToken cancellationToken = default);

    Task<Guid> SaveRunAsync(
        string useCase,
        string inputRefsJson,
        string outcome,
        CancellationToken cancellationToken = default);
}
