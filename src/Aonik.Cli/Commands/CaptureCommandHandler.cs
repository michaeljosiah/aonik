using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Drives the <c>capture</c> command group against the Spec 047
/// <c>POST /ai/capture/parse</c> endpoint — turns text or an image into a
/// structured draft proposal (never persisted; the user confirms before a
/// PaymentLog is written).
/// </summary>
public sealed class CaptureCommandHandler
{
    private static readonly JsonSerializerOptions HintsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public CaptureCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> ParseAsync(CaptureParseOptions options, CancellationToken cancellationToken = default)
    {
        var hasText = !string.IsNullOrWhiteSpace(options.Text);
        var hasImage = !string.IsNullOrWhiteSpace(options.ImagePath);

        if (hasText == hasImage)
        {
            throw new AonikCliException("Provide exactly one of '--text' or '--image'.");
        }

        string inputType;
        string payload;
        if (hasImage)
        {
            if (!File.Exists(options.ImagePath))
            {
                throw new AonikCliException($"Image file not found: {options.ImagePath}");
            }

            var bytes = await File.ReadAllBytesAsync(options.ImagePath!, cancellationToken);
            payload = Convert.ToBase64String(bytes);
            inputType = CaptureInputTypes.Image;
        }
        else
        {
            payload = options.Text!;
            inputType = string.IsNullOrWhiteSpace(options.InputType) ? CaptureInputTypes.Text : options.InputType;
        }

        CaptureHints? hints = null;
        if (!string.IsNullOrWhiteSpace(options.HintsJson))
        {
            try
            {
                hints = JsonSerializer.Deserialize<CaptureHints>(options.HintsJson!, HintsJsonOptions);
            }
            catch (JsonException ex)
            {
                throw new AonikCliException($"'--hints-json' is not valid JSON: {ex.Message}");
            }
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ParseCaptureAsync(
            session,
            new CaptureParseRequest(inputType, payload, hints),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
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

/// <summary>Allowed capture input types (CLI mirror of the server contract).</summary>
public static class CaptureInputTypes
{
    public const string Image = "image";
    public const string Text = "text";
    public const string AudioTranscript = "audioTranscript";
}
