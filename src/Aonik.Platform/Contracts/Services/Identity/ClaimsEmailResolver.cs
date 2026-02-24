using System.Security.Claims;

namespace Aonik.Platform.Contracts.Services.Identity;

public static class ClaimsEmailResolver
{
    private static readonly string[] EmailClaimTypes =
    [
        "email",
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
            var value = principal.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (claimType == ClaimTypes.Name && !value.Contains('@', StringComparison.Ordinal))
            {
                continue;
            }

            return value;
        }

        return null;
    }
}
