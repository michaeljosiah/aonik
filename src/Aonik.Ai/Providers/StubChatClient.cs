using Microsoft.Extensions.AI;

namespace Aonik.Ai.Providers;

/// <summary>
/// Stub implementation of <see cref="IChatClient"/> that returns placeholder responses.
/// Replaces the old IModelProvider / StubModelProvider during migration to Microsoft.Extensions.AI.
/// In production, this will be replaced with real LLM provider clients (Azure OpenAI, OpenAI, etc.).
/// </summary>
internal sealed class StubChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = new("StubChatClient");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var messageList = chatMessages.ToList();
        var systemPrompt = messageList
            .FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";
        var userPrompt = messageList
            .FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";

        var completion = $@"[STUB AI RESPONSE]

This is a placeholder AI response. In a production environment, this would connect to:
- Azure OpenAI Service
- OpenAI API
- Microsoft Agent Framework
- or another LLM provider

The model would process the system prompt and user prompt to generate a real insight.

Received System Prompt: {systemPrompt[..Math.Min(100, systemPrompt.Length)]}...
Received User Prompt: {userPrompt[..Math.Min(100, userPrompt.Length)]}...";

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, completion)]);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming is not supported by the stub chat client.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IChatClient))
            return this;
        return null;
    }

    public void Dispose() { }
}
