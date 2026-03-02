namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Contract for persisting AI run execution metadata.
/// Implemented by the AI module and consumed by domain workflows.
/// </summary>
public interface IAiRunWriter
{
    Task<Guid> SaveRunAsync(
        string useCase,
        string inputRefsJson,
        string outcome,
        CancellationToken cancellationToken = default);
}
