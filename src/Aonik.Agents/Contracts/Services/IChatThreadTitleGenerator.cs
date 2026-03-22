namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Generates a short, descriptive title for a chat thread based on the
/// first user message. Uses an LLM call via IChatClient.
/// </summary>
public interface IChatThreadTitleGenerator
{
    /// <summary>
    /// Generates a title for the given user message. Returns a short string
    /// (max ~8 words). Falls back to a truncated version of the message on failure.
    /// </summary>
    Task<string> GenerateTitleAsync(
        string firstUserMessage,
        CancellationToken cancellationToken = default);
}
