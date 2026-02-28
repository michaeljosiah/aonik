using System.Security.Claims;
using System.Text.Json;

namespace Aonik.Platform.Contracts.Services.Identity;

public static class ClaimsEmailResolver
{
    private static readonly string[] EmailClaimTypes =
    [
        "email",
        "https://aonik.com/email",
        "preferred_username",
        "upn",
        "https://aonik.app/email",
        ClaimTypes.Email,
        ClaimTypes.Upn,
        ClaimTypes.Name
    ];

    public static string? GetEmail(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return null;
        }

        foreach (var claimType in EmailClaimTypes)
        {
            var claimValues = principal.Claims
                .Where(c => c.Type == claimType)
                .Select(c => c.Value)
                .ToList();

            if (claimValues.Count == 0)
            {
                continue;
            }

            foreach (var claimValue in claimValues)
            {
                var normalizedEmail = NormalizeEmailClaimValue(claimValue);
                if (normalizedEmail == null)
                {
                    continue;
                }

                return normalizedEmail;
            }
        }

        return null;
    }

    private static string? NormalizeEmailClaimValue(string? claimValue)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        var trimmed = claimValue.Trim();

        if (LooksLikeEmail(trimmed))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(trimmed);
                var firstEmail = values?
                    .Select(value => value?.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && LooksLikeEmail(value));

                return firstEmail;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static bool LooksLikeEmail(string value)
    {
        return value.Contains('@', StringComparison.Ordinal)
               && !value.Contains(' ', StringComparison.Ordinal)
               && !value.StartsWith("{", StringComparison.Ordinal)
               && !value.EndsWith("}", StringComparison.Ordinal);
    }
}
