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
