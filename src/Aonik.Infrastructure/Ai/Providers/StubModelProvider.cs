using Aonik.Application.Abstractions.Ai;

namespace Aonik.Infrastructure.Ai.Providers;

public class StubModelProvider : IModelProvider
{
    public Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        // Stub implementation - returns placeholder text
        // In production, this would call Azure OpenAI, OpenAI API, or Microsoft Agent Framework
        var completion = $@"[STUB AI RESPONSE]

This is a placeholder AI response. In a production environment, this would connect to:
- Azure OpenAI Service
- OpenAI API
- Microsoft Agent Framework
- or another LLM provider

The model would process the system prompt and user prompt to generate a real insight.

Received System Prompt: {systemPrompt.Substring(0, Math.Min(100, systemPrompt.Length))}...
Received User Prompt: {userPrompt.Substring(0, Math.Min(100, userPrompt.Length))}...";

        return Task.FromResult(completion);
    }
}
