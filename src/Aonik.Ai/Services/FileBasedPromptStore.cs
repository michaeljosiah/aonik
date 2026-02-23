using Aonik.Ai.Contracts.Services;

namespace Aonik.Ai.Services;

/// <summary>
/// Loads prompt templates from .md files on disk.
/// File naming convention: {promptName}.{version}.{role}.md
/// </summary>
internal sealed class FileBasedPromptStore : IPromptStore
{
    private readonly string _promptTemplatesPath;

    public FileBasedPromptStore(string? promptTemplatesPath = null)
    {
        _promptTemplatesPath = promptTemplatesPath ??
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompting", "Templates");
    }

    public async Task<string> LoadPromptAsync(string promptName, string version = "v1", string role = "system", CancellationToken cancellationToken = default)
    {
        var fileName = $"{promptName}.{version}.{role}.md";
        var filePath = Path.Combine(_promptTemplatesPath, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Prompt template not found: {fileName}", filePath);
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
}
