using Microsoft.Agents.AI;

namespace Aonik.Agents.Framework;

/// <summary>Preserves the tenant script gate when MAF advertises script tools for scriptless sources.</summary>
internal sealed class ReadOnlySkillsContextProvider(AgentSkillsProvider inner) : AIContextProvider, IDisposable
{
    protected override async ValueTask<AIContext> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        AIContext result = await inner.InvokingAsync(context, cancellationToken);
        result.Tools = result.Tools?
            .Where(tool => tool.Name != AgentSkillsProvider.RunSkillScriptToolName)
            .ToList();
        return result;
    }

    protected override ValueTask InvokedCoreAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
        => inner.InvokedAsync(context, cancellationToken);

    public void Dispose() => inner.Dispose();
}
