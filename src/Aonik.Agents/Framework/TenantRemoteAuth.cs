using System.Text;
using System.Text.Json;
using Aonik.Agents.Entities;

namespace Aonik.Agents.Framework;

/// <summary>
/// Translates a tenant tool's <see cref="TenantToolAuthKind"/> + encrypted secret blob into the
/// HTTP headers to attach to an outbound MCP connection or HTTP tool call. The blob is decrypted
/// here, server-side, at call time only (Spec 033 §11). Shared by the remote MCP provider (§8.3)
/// and the declarative HTTP tool (§8.4).
/// <para>
/// Expected decrypted JSON shapes:
/// <c>BearerToken</c> → <c>{"token":"..."}</c>;
/// <c>ApiKeyHeader</c> → <c>{"header":"X-Api-Key","value":"..."}</c>;
/// <c>Basic</c> → <c>{"username":"...","password":"..."}</c>.
/// </para>
/// </summary>
internal static class TenantRemoteAuth
{
    public static IDictionary<string, string> BuildHeaders(
        TenantToolAuthKind kind,
        string? protectedAuthJson,
        ITenantCredentialProtector protector)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (kind == TenantToolAuthKind.None || string.IsNullOrWhiteSpace(protectedAuthJson))
        {
            return headers;
        }

        JsonElement root;
        try
        {
            var json = protector.Unprotect(protectedAuthJson);
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
        }
        catch
        {
            // A secret that won't decrypt/parse must not crash the agent build; the call will simply
            // be unauthenticated and likely rejected by the remote — fail safe, not open.
            return headers;
        }

        switch (kind)
        {
            case TenantToolAuthKind.BearerToken:
                var token = GetString(root, "token");
                if (!string.IsNullOrEmpty(token))
                {
                    headers["Authorization"] = $"Bearer {token}";
                }
                break;

            case TenantToolAuthKind.ApiKeyHeader:
                var header = GetString(root, "header");
                var value = GetString(root, "value");
                if (!string.IsNullOrEmpty(header) && value is not null)
                {
                    headers[header] = value;
                }
                break;

            case TenantToolAuthKind.Basic:
                var username = GetString(root, "username");
                var password = GetString(root, "password");
                if (!string.IsNullOrEmpty(username))
                {
                    var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                    headers["Authorization"] = $"Basic {basic}";
                }
                break;
        }

        return headers;
    }

    /// <summary>
    /// Build the plaintext auth JSON for the given kind from operator-supplied fields, to be encrypted
    /// by the caller before storage. Returns null when there is no secret to store.
    /// </summary>
    public static string? BuildAuthJson(TenantToolAuthKind kind, string? secret, string? username, string? headerName)
    {
        return kind switch
        {
            TenantToolAuthKind.BearerToken when !string.IsNullOrWhiteSpace(secret)
                => JsonSerializer.Serialize(new { token = secret }),
            TenantToolAuthKind.ApiKeyHeader when !string.IsNullOrWhiteSpace(headerName) && !string.IsNullOrWhiteSpace(secret)
                => JsonSerializer.Serialize(new { header = headerName, value = secret }),
            TenantToolAuthKind.Basic when !string.IsNullOrWhiteSpace(username)
                => JsonSerializer.Serialize(new { username, password = secret ?? string.Empty }),
            _ => null,
        };
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var prop)
        && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
