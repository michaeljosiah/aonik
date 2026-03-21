using Microsoft.Agents.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Factory for building a workflow agent. Registered as a keyed service
/// where the key is the workflow name (e.g., "invoice-processing").
/// This replaces the switch-based workflow resolution pattern (R10).
/// </summary>
public interface IWorkflowFactory
{
    /// <summary>The canonical workflow name used as the keyed service key.</summary>
    string WorkflowName { get; }

    /// <summary>
    /// Builds the workflow as an <see cref="AIAgent"/> ready for execution.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies (e.g., IChatClient).</param>
    AIAgent Build(IServiceProvider serviceProvider);
}
