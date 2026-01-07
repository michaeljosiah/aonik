using Aonik.Application.Abstractions.Ai;

namespace Aonik.Infrastructure.Ai.Prompting;

public class FileBasedPromptStore : IPromptStore
{
    private readonly string _promptTemplatesPath;

    public FileBasedPromptStore(string? promptTemplatesPath = null)
    {
        _promptTemplatesPath = promptTemplatesPath ?? 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ai", "Prompting", "Templates");
    }

    public async Task<string> LoadPromptAsync(string promptName, string version = "v1", string role = "system", CancellationToken cancellationToken = default)
    {
        // File naming convention: {promptName}.{version}.{role}.md
        var fileName = $"{promptName}.{version}.{role}.md";
        var filePath = Path.Combine(_promptTemplatesPath, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Prompt template not found: {fileName}", filePath);
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
}
