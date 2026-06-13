using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Drives the <c>care-entities</c> command group against the Spec 043
/// /personal-finance/care-entities endpoints (UserPolicy / PersonalUser).
/// </summary>
public sealed class CareEntityCommandHandler
{
    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public CareEntityCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> ListAsync(ListCareEntitiesOptions options, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ListCareEntitiesAsync(
            session, options.Kind, options.AssetType, options.IncludeArchived, cancellationToken);
        await _outputWriter.WriteCollectionAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.GetCareEntityAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CreateAsync(CreateCareEntityOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Kind)
            || string.IsNullOrWhiteSpace(options.Name)
            || string.IsNullOrWhiteSpace(options.CountryCode))
        {
            throw new AonikCliException("'--kind', '--name', and '--country' are required.");
        }

        var attributes = await ReadJsonObjectAsync(options.AttributesFile, cancellationToken);
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.CreateCareEntityAsync(
            session,
            new CreateCareEntityRequest(
                options.Kind,
                options.AssetType,
                options.Name,
                options.CountryCode,
                options.Relationship,
                options.Emoji,
                options.PhotoDocumentId,
                attributes),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> UpdateAsync(UpdateCareEntityOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(options.Name)
            || string.IsNullOrWhiteSpace(options.CountryCode))
        {
            throw new AonikCliException("'<id>', '--name', and '--country' are required.");
        }

        var attributes = await ReadJsonObjectAsync(options.AttributesFile, cancellationToken);
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.UpdateCareEntityAsync(
            session,
            options.Id,
            new UpdateCareEntityRequest(
                options.Name,
                options.AssetType,
                options.CountryCode,
                options.Relationship,
                options.Emoji,
                options.PhotoDocumentId,
                attributes),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ArchiveAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _apiClient.ArchiveCareEntityAsync(session, id, cancellationToken);
        await _outputWriter.WriteInfoAsync($"Care entity {id:D} archived.", cancellationToken);
        return 0;
    }

    public async Task<int> ProfileAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.GetCareEntityProfileAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    private static async Task<IReadOnlyDictionary<string, string>?> ReadJsonObjectAsync(
        string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new AonikCliException($"File not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonReadOptions);
        }
        catch (JsonException ex)
        {
            throw new AonikCliException($"Failed to parse JSON file '{path}': {ex.Message}");
        }
    }

    private async Task<CliSession> RequireSessionAsync(CancellationToken cancellationToken)
    {
        var session = await _sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            throw new AonikCliException("No active session found. Run 'aonik auth login' first.");
        }

        return session;
    }
}
