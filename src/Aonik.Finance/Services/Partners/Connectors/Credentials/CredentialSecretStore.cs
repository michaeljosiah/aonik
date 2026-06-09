using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Finance.Services.Partners.Connectors.Credentials;

/// <summary>
/// In-memory shape of a <see cref="Aonik.Finance.Entities.Partners.CredentialBundle"/>'s decrypted secret
/// payload (Spec 042 §6, §11). Most fields are plain current values; verifier fields (the webhook signing
/// secret) additionally carry a rotation entry so a webhook signed with the previous secret still validates
/// within the grace window. This object is the JSON that gets encrypted into <c>ProtectedSecretsJson</c> —
/// it must never be logged or returned to a client.
/// </summary>
internal sealed class CredentialSecretStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>fieldName → current secret value.</summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>fieldName → previous-secret rotation entry (verifier fields only).</summary>
    [JsonPropertyName("rotations")]
    public Dictionary<string, RotatedSecret> Rotations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetCurrent(string field) =>
        Fields.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public bool Has(string field) => GetCurrent(field) is not null;

    /// <summary>
    /// Returns the candidate secrets to verify a signature against: the current value plus, when a rotation
    /// is in flight and still within its window (evaluated at <paramref name="now"/> — read-time expiry,
    /// §11), the previous value. Expired previous values are silently excluded.
    /// </summary>
    public IReadOnlyList<string> GetVerificationCandidates(string field, DateTime now)
    {
        var candidates = new List<string>();
        var current = GetCurrent(field);
        if (current is not null)
        {
            candidates.Add(current);
        }

        if (Rotations.TryGetValue(field, out var rotated)
            && !string.IsNullOrWhiteSpace(rotated.Previous)
            && now < rotated.ExpiresAt)
        {
            candidates.Add(rotated.Previous);
        }

        return candidates;
    }

    /// <summary>Sets a field's current value, clearing any in-flight rotation for it.</summary>
    public void Set(string field, string value)
    {
        Fields[field] = value;
        Rotations.Remove(field);
    }

    /// <summary>
    /// Rotates a verifier field: the existing current becomes <c>previous</c> with the supplied expiry, and
    /// <paramref name="newValue"/> becomes current (Spec 042 §11). A no-op grace when there was no prior value.
    /// </summary>
    public void Rotate(string field, string newValue, DateTime previousExpiresAt)
    {
        var existing = GetCurrent(field);
        if (existing is not null)
        {
            Rotations[field] = new RotatedSecret { Previous = existing, ExpiresAt = previousExpiresAt };
        }

        Fields[field] = newValue;
    }

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public static CredentialSecretStore Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new CredentialSecretStore()
            : JsonSerializer.Deserialize<CredentialSecretStore>(json, SerializerOptions) ?? new CredentialSecretStore();
}

/// <summary>A previous secret value retained during a rotation window (Spec 042 §11).</summary>
internal sealed class RotatedSecret
{
    [JsonPropertyName("previous")]
    public string Previous { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}
