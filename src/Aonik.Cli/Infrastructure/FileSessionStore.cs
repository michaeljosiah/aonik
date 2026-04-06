using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Infrastructure;

public sealed class FileSessionStore : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _sessionFilePath;

    public FileSessionStore(string? sessionFilePath = null)
    {
        _sessionFilePath = sessionFilePath ?? ResolveDefaultPath();
    }

    public async Task<CliSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_sessionFilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_sessionFilePath);
        return await JsonSerializer.DeserializeAsync<CliSession>(stream, JsonOptions, cancellationToken);
    }

    public async Task SaveAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_sessionFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_sessionFilePath);
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (File.Exists(_sessionFilePath))
        {
            File.Delete(_sessionFilePath);
        }

        return Task.CompletedTask;
    }

    private static string ResolveDefaultPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("AONIK_CLI_SESSION_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "Aonik", "cli-session.json");
    }
}
