using System.Text.Json;

namespace Aonik.Platform.Services.Identity;

internal static class TenantCountryCodeSerializer
{
    public static string[] Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<string[]>(json);
            if (items == null)
            {
                return Array.Empty<string>();
            }

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    public static string[] ResolveWithFallback(string? configuredJson, IReadOnlyCollection<string> fallbackCountryCodes)
    {
        var configured = Deserialize(configuredJson);
        if (configured.Length > 0 || string.Equals(configuredJson?.Trim(), "[]", StringComparison.Ordinal))
        {
            return configured;
        }

        return fallbackCountryCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string Serialize(IEnumerable<string> countryCodes)
    {
        var normalized = countryCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(normalized);
    }
}
