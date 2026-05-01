namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Reserved for future message-routing needs. The current iteration
/// passes plain <c>string</c> between executors so the
/// <see cref="Microsoft.Agents.AI.Workflows.Workflow"/> can be wrapped
/// as an <c>AIAgent</c> and accept a string user-message as input. When
/// we add per-edge routing (decisions, loops) we'll lift this up to a
/// richer envelope.
/// </summary>
internal static class GraphPayloads
{
    public const string Empty = "";
}
