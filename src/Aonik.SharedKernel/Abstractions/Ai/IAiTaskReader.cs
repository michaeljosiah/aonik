namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Read-only cross-module contract for fetching AI task definitions by ID.
/// Used by the playground endpoint (Agents module) to resolve task templates
/// without a direct reference to the Ai module.
/// </summary>
public interface IAiTaskReader
{
    /// <summary>
    /// Returns a lightweight AI task summary for playground / cross-module use.
    /// Returns <c>null</c> if the task does not exist or is not published.
    /// </summary>
    Task<AiTaskSnapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight snapshot of an AI task definition used for cross-module reads.
/// </summary>
public sealed record AiTaskSnapshot(
    Guid Id,
    string UseCase,
    string DisplayName,
    string? SystemTemplate,
    string? UserTemplate,
    string? DeveloperTemplate,
    string? VariablesSchemaJson);
