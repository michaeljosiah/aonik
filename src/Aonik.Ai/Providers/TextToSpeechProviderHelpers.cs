using System.Text.Json;

namespace Aonik.Ai.Providers;

/// <summary>
/// Shared helpers for TTS provider implementations to avoid duplicating
/// error parsing, truncation, and error message formatting across providers.
/// </summary>
internal static class TextToSpeechProviderHelpers
{
    private const int MaxErrorBodyLength = 300;

    public static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= MaxErrorBodyLength ? value : value[..MaxErrorBodyLength];
    }

    public static string BuildErrorMessage(string providerName, string operation, System.Net.HttpStatusCode statusCode, string? errorBody)
    {
        var detail = TryExtractErrorMessage(errorBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{providerName} {operation} failed with status {(int)statusCode}."
            : detail;
    }

    public static string? TryExtractErrorMessage(string? errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (root.TryGetProperty("detail", out var detail))
            {
                if (detail.ValueKind == JsonValueKind.String)
                    return detail.GetString();
                if (detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("message", out var detailMessage))
                    return detailMessage.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall back to raw payload below.
        }

        return Truncate(errorBody);
    }
}
