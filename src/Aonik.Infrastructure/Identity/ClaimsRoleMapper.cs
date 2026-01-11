using System.Security.Claims;

namespace Aonik.Infrastructure.Identity;

public static class ClaimsRoleMapper
{
    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "roles",
        "role"
    ];

    public static IReadOnlyCollection<string> ExtractRoles(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return Array.Empty<string>();
        }

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in principal.Claims)
        {
            if (!RoleClaimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var role in SplitRoles(claim.Value))
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    roles.Add(role.Trim());
                }
            }
        }

        return roles.Count == 0 ? Array.Empty<string>() : roles.ToArray();
    }

    private static IEnumerable<string> SplitRoles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        if (value.Contains(','))
        {
            foreach (var role in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return role;
            }

            yield break;
        }

        if (value.Contains(' '))
        {
            foreach (var role in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return role;
            }

            yield break;
        }

        yield return value;
    }
}
