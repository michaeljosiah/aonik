using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Produces a <see cref="GraphWorkflowFactory"/> for a given slug. Used by
/// <see cref="Endpoints.RunWorkflowEndpoint"/> as a fallback when no
/// keyed legacy factory matches — every saved editor workflow becomes
/// runnable through this path.
/// </summary>
public interface IGraphWorkflowFactoryProvider
{
    IWorkflowFactory For(string slug);
}

internal sealed class GraphWorkflowFactoryProvider : IGraphWorkflowFactoryProvider
{
    public IWorkflowFactory For(string slug) => new GraphWorkflowFactory(slug);
}
