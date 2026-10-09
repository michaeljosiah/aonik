using System.Text.Json;

namespace Aonik.Finance.Services.Partners.Connectors.Registry;

/// <summary>
/// Parses and validates a connector's non-secret <c>ConfigJson</c> against its kind's config schema
/// (Spec 042 §10). Config is a flat string map (e.g. <c>{ "environment": "sandbox" }</c>); transport
/// endpoints are NOT operator-authored — they are derived from <c>environment</c> by the connector code.
/// </summary>
internal static class ConnectorConfigJson
{
    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Connector ConfigJson must be a JSON object.");
        }

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => property.Value.ToString(),
                JsonValueKind.Null => string.Empty,
                _ => property.Value.GetRawText(),
            };
        }

        return result;
    }

    /// <summary>
    /// Validates <paramref name="json"/> against the kind's config schema: rejects unknown keys, enforces
    /// the allowed-value set for enum-like fields (e.g. <c>environment</c>), and requires required fields.
    /// Throws <see cref="InvalidOperationException"/> on any violation (Spec 042 §10).
    /// </summary>
    public static void Validate(ConnectorKindDescriptor descriptor, string? json)
    {
        var values = Parse(json);

        foreach (var (key, value) in values)
        {
            var field = descriptor.Config(key);
            if (field is null)
            {
                throw new InvalidOperationException(
                    $"Config key '{key}' is not valid for connector kind '{descriptor.Kind}'.");
            }

            if (field.AllowedValues is { Count: > 0 }
                && !string.IsNullOrWhiteSpace(value)
                && !field.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Config '{key}' must be one of [{string.Join(", ", field.AllowedValues)}] "
                    + $"for connector kind '{descriptor.Kind}'.");
            }
        }

        foreach (var field in descriptor.ConfigFields.Where(f => f.Required))
        {
            if (!values.TryGetValue(field.Name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Config '{field.Name}' is required for connector kind '{descriptor.Kind}'.");
            }
        }

        if (descriptor.Kind == ConnectorRegistry.StripeCheckoutV1)
        {
            var accountId = values[ConnectorRegistry.ConfigAccountId];
            if (!accountId.StartsWith("acct_", StringComparison.Ordinal)
                || accountId.Length is < 6 or > 100
                || accountId.Skip(5).Any(c => !char.IsAsciiLetterOrDigit(c)))
            {
                throw new InvalidOperationException("A Stripe merchant account ID is required.");
            }

            var origin = values[ConnectorRegistry.ConfigReturnOrigin];
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException("Stripe returnOrigin must be an HTTPS origin without a path, query or credentials.");
            }
        }
    }

    public static void ValidateUpdate(string oldKind, string oldJson, string newKind, string newJson)
    {
        if (!string.Equals(oldKind, ConnectorRegistry.StripeCheckoutV1, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var oldValues = Parse(oldJson);
        var newValues = Parse(newJson);
        if (!string.Equals(oldKind, newKind, StringComparison.OrdinalIgnoreCase)
            || !oldValues.TryGetValue(ConnectorRegistry.ConfigAccountId, out var oldAccount)
            || !newValues.TryGetValue(ConnectorRegistry.ConfigAccountId, out var newAccount)
            || !string.Equals(oldAccount, newAccount, StringComparison.Ordinal)
            || !oldValues.TryGetValue(ConnectorRegistry.ConfigEnvironment, out var oldEnvironment)
            || !newValues.TryGetValue(ConnectorRegistry.ConfigEnvironment, out var newEnvironment)
            || !string.Equals(oldEnvironment, newEnvironment, StringComparison.OrdinalIgnoreCase)
            || !oldValues.TryGetValue(ConnectorRegistry.ConfigReturnOrigin, out var oldOrigin)
            || !newValues.TryGetValue(ConnectorRegistry.ConfigReturnOrigin, out var newOrigin)
            || !Uri.TryCreate(oldOrigin, UriKind.Absolute, out var oldUri)
            || !Uri.TryCreate(newOrigin, UriKind.Absolute, out var newUri)
            || !string.Equals(oldUri.GetLeftPart(UriPartial.Authority), newUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A Stripe connector's kind, merchant account, environment and return origin cannot change. Create a new connector instead.");
        }
    }
}
