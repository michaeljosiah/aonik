namespace Aonik.Agents.Entities;

/// <summary>
/// Classifies an agent configuration row as either a top-level orchestrator
/// or a sub-agent (domain agent that an orchestrator delegates to).
/// </summary>
public enum AgentType
{
    /// <summary>
    /// A domain specialist that is invoked as a tool by an orchestrator.
    /// Corresponds to concrete <see cref="Contracts.Services.IDomainAgentDescriptor"/> registrations.
    /// </summary>
    SubAgent = 0,

    /// <summary>
    /// A top-level orchestrator that routes user messages to sub-agents.
    /// Examples: master-orchestrator (Admin UI), personal-finance-orchestrator (Payabo).
    /// </summary>
    Orchestrator = 1,
}
