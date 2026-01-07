namespace Aonik.Application.Abstractions.Ai;

public interface IPromptStore
{
    Task<string> LoadPromptAsync(string promptName, string version = "v1", string role = "system", CancellationToken cancellationToken = default);
}
