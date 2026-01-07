namespace Aonik.Application.Abstractions.Ai;

public interface IModelProvider
{
    Task<string> GenerateCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
