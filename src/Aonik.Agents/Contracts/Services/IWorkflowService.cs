using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Authorized tenant workflow drafts, immutable version history, validation and fixture simulation.
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Lists all workflows visible to the current tenant. Aggregates the
    /// "runs today" / weighted success / average duration figures the
    /// list page's KPI strip + per-row footer need, plus the inline step
    /// rail (compact node list).
    /// </summary>
    Task<IReadOnlyList<WorkflowSummaryResponse>> ListAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the full graph (nodes + edges + comments) for the editor.
    /// Lookup is by slug because that's what the route uses
    /// (<c>/ai/workflows/match_and_apply</c>); accepting the slug here means
    /// the SPA never needs the canonical Guid.
    /// </summary>
    Task<WorkflowGraphResponse?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists recent runs for a workflow (most-recent first). The detail rail
    /// shows the top six; the editor's trace replay walks the same list.
    /// </summary>
    Task<IReadOnlyList<WorkflowRunResponse>> ListRunsAsync(
        Guid workflowId,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the version history for a workflow (newest first).
    /// </summary>
    Task<IReadOnlyList<WorkflowVersionResponse>> ListVersionsAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an unpublished draft and its initial immutable snapshot. A reserved slug conflicts.
    /// </summary>
    Task<WorkflowGraphResponse> CreateAsync(
        WorkflowSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowGraphResponse?> UpdateAsync(
        WorkflowSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowValidation?> ValidateAsync(string slug, CancellationToken cancellationToken = default);

    Task<WorkflowSimulation?> SimulateAsync(
        string slug, WorkflowSimulationRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the workflow (and its nodes/edges/comments). Runs
    /// and version history are preserved.
    /// </summary>
    Task<bool> DeleteAsync(
        string slug,
        string? expectedVersion,
        CancellationToken cancellationToken = default);
}
