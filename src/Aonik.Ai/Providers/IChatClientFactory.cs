using Microsoft.Extensions.AI;

namespace Aonik.Ai.Providers;

/// <summary>
/// Factory interface for creating <see cref="IChatClient"/> instances based on
/// configuration. Allows the AI module to resolve the correct LLM provider
/// (stub, OpenAI, Azure OpenAI, etc.) at runtime.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Creates an <see cref="IChatClient"/> for the configured provider.
    /// The returned client can be wrapped with middleware (audit, proposal)
    /// before being passed to agents.
    /// </summary>
    IChatClient CreateClient();
}
