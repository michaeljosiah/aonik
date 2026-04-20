using System.Text.Json;
using Aonik.Agents.Contracts.Agui;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Translates between the AG-UI wire-protocol DTOs and
/// <see cref="Microsoft.Extensions.AI"/> types used by the agent runtime.
/// </summary>
public interface IAguiMessageConverter
{
    /// <summary>
    /// Converts AG-UI input messages to M.E.AI <see cref="ChatMessage"/> objects,
    /// preserving assistant tool calls and tool-result messages with CallId references.
    /// </summary>
    List<ChatMessage> ConvertMessages(IEnumerable<AguiMessage>? messages);

    /// <summary>
    /// Converts raw AG-UI tool JSON elements into declaration-only
    /// <see cref="AITool"/> instances. The LLM can see these tools but cannot
    /// invoke them server-side — the frontend handles execution via the re-run loop.
    /// </summary>
    List<AITool> ConvertClientTools(List<JsonElement>? toolElements);
}
