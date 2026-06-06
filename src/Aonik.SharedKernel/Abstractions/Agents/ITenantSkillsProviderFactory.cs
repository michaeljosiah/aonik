using Microsoft.Agents.AI;

namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Builds the current tenant's <c>AgentSkillsProvider</c> (an <see cref="AIContextProvider"/>)
/// from its active, approved skills (Spec 033 §8.1), to attach via
/// <c>ChatClientAgentOptions.AIContextProviders</c> at the descriptor's one build seam (§8.6).
/// <para>
/// Lives on SharedKernel so a domain module can attach tenant skills without referencing the Agents
/// runtime. Returns <see langword="null"/> when the tenant has no active skills, so the descriptor
/// adds no context providers and the agent builds exactly as before.
/// </para>
/// </summary>
public interface ITenantSkillsProviderFactory
{
    /// <summary>
    /// Create an <see cref="AIContextProvider"/> exposing the current tenant's active skills, or
    /// <see langword="null"/> if there are none / no resolvable tenant.
    /// </summary>
    AIContextProvider? Create(IServiceProvider serviceProvider);
}
