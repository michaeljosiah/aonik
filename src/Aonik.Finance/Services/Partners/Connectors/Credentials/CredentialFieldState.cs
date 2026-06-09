using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Finance.Services.Partners.Connectors.Credentials;

/// <summary>
/// Plaintext, value-free description of one credential field's state (Spec 042 §6). Persisted in
/// <see cref="Aonik.Finance.Entities.Partners.CredentialBundle.FieldMetadataJson"/> so the read API can render
/// "Configured" / "Not set" badges and show the rotation version without decrypting any secret.
/// </summary>
internal sealed record CredentialFieldState(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isSet")] bool IsSet,
    [property: JsonPropertyName("version")] int Version);

/// <summary>Serialization helpers for the field-metadata list.</summary>
internal static class CredentialFieldMetadata
{
    public static string Serialize(IEnumerable<CredentialFieldState> states) =>
        JsonSerializer.Serialize(states.ToList());

    public static IReadOnlyList<CredentialFieldState> Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<CredentialFieldState>()
            : JsonSerializer.Deserialize<List<CredentialFieldState>>(json) ?? new List<CredentialFieldState>();
}
