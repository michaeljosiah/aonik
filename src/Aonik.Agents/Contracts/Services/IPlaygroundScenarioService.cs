using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Manages playground scenarios — saved, reusable conversation setups
/// for testing agents and AI tasks in the playground.
/// </summary>
public interface IPlaygroundScenarioService
{
    /// <summary>
    /// Lists all scenarios for the current tenant, optionally filtered by tag.
    /// Returns summary DTOs (no turns) for efficiency.
    /// </summary>
    Task<IReadOnlyList<PlaygroundScenarioSummaryResponse>> ListAsync(
        string? agentName = null,
        string? tag = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single scenario by ID, including its turns.
    /// </summary>
    Task<PlaygroundScenarioResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new scenario from the provided request.
    /// </summary>
    Task<PlaygroundScenarioResponse> CreateAsync(
        CreatePlaygroundScenarioRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing scenario. Supports partial updates.
    /// If turns are provided, they replace the existing turns entirely.
    /// </summary>
    Task<PlaygroundScenarioResponse?> UpdateAsync(
        Guid id,
        UpdatePlaygroundScenarioRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a scenario by ID (soft-delete).
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
