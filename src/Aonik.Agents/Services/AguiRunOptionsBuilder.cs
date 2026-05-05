using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Services;

/// <summary>
/// Combines client tool declarations with the agent's configured model
/// override into a <see cref="ChatClientAgentRunOptions"/>. The configured
/// model lives on <c>AnkAgents.AiModelId</c> and is resolved by
/// <c>IAgentContextualizer</c>; without it agents silently inherit the
/// chat client's global default (the bug a dev trace surfaced for the
/// personal-finance agent).
/// </summary>
internal sealed class AguiRunOptionsBuilder : IAguiRunOptionsBuilder
{
    public ChatClientAgentRunOptions? Build(
        IReadOnlyList<AITool> clientTools,
        string? configuredModelName)
    {
        var hasTools = clientTools is { Count: > 0 };
        var hasModelOverride = !string.IsNullOrWhiteSpace(configuredModelName);

        if (!hasTools && !hasModelOverride)
            return null;

        var chatOptions = new ChatOptions();
        if (hasTools)
            chatOptions.Tools = [.. clientTools];
        if (hasModelOverride)
            chatOptions.ModelId = configuredModelName;

        return new ChatClientAgentRunOptions { ChatOptions = chatOptions };
    }
}
