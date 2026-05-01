using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Workflow registry + run history access. Read paths back the editor and
/// the registry list page; mutating paths back the editor's save / delete
/// actions and replace the whole graph (full delete-then-insert of nodes
/// and edges) on each save.
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
    /// Persists the editor graph. If a workflow with the request's slug
    /// exists, replaces its nodes and edges, snapshots the prior graph
    /// into a <c>WorkflowVersion</c> row, and bumps the version tag.
    /// Otherwise creates a new workflow row plus its initial version
    /// snapshot. Returns the freshly-loaded graph (with canonical Guids)
    /// so the editor can rehydrate.
    /// </summary>
    Task<WorkflowGraphResponse> SaveAsync(
        WorkflowSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the workflow (and its nodes/edges/comments). Runs
    /// and version history are preserved.
    /// </summary>
    Task<bool> DeleteAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
