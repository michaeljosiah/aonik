namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Cross-module cleanup surface used by the Platform demo-seed reverse flow
/// to remove agent-side rows (proposals, agent runs, workflows + their
/// dependents, agents) without touching the Agents runtime's DbContext
/// directly. Implemented inside the Agents runtime; consumed by
/// <c>Aonik.Platform.Services.Seeding.DemoSeedService</c>.
/// </summary>
public interface IAgentDemoCleanup
{
    /// <summary>
    /// Removes every <c>Proposal</c> + <c>AgentRun</c> produced by an agent
    /// whose <c>Name</c> is in <paramref name="agentNames"/>, scoped to
    /// <paramref name="tenantId"/>. Used during the reverse-seed phase.
    /// </summary>
    Task<AgentActivityCleanupCounts> RemoveAgentActivityAsync(
        Guid tenantId,
        IReadOnlyCollection<string> agentNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the workflows whose <c>Slug</c> is in
    /// <paramref name="workflowSlugs"/> along with their dependents
    /// (versions, runs, comments, edges, nodes), then removes any
    /// <c>Agent</c> whose <c>Name</c> is in <paramref name="agentNames"/>,
    /// scoped to <paramref name="tenantId"/>. Order matters — workflows are
    /// purged before agents because workflows reference agents.
    /// </summary>
    Task<WorkflowRegistryCleanupCounts> RemoveWorkflowsAndAgentsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> workflowSlugs,
        IReadOnlyCollection<string> agentNames,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Counts returned from
/// <see cref="IAgentDemoCleanup.RemoveAgentActivityAsync"/>. Used by the
/// caller to log a human-readable summary line.
/// </summary>
public sealed record AgentActivityCleanupCounts(
    int ProposalsDeleted,
    int AgentRunsDeleted);

/// <summary>
/// Counts returned from
/// <see cref="IAgentDemoCleanup.RemoveWorkflowsAndAgentsAsync"/>.
/// </summary>
public sealed record WorkflowRegistryCleanupCounts(
    int WorkflowsDeleted,
    int AgentsDeleted);
