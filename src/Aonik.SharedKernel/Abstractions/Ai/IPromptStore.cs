namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Contract for loading versioned prompt templates.
/// Implemented by the AI module. Consumed by domain modules that need AI prompts.
/// </summary>
public interface IPromptStore
{
    Task<string> LoadPromptAsync(string promptName, string version = "v1", string role = "system", CancellationToken cancellationToken = default);
}
