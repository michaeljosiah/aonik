using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Serialization;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal sealed record VerifiedConnectorPackage(ConnectorPackageManifest Manifest, string Digest, string ArtifactJson)
{
    public ConnectorArtifactPin Pin(ConnectorPackageAction action)
        => new(Manifest.PackageKey, Manifest.PackageVersion, Digest, CanonicalJson.Hash(JsonSerializer.SerializeToElement(new
        { Manifest.PackageKey, Manifest.PublisherId, Manifest.ConnectorKey, Manifest.AdapterKind, Manifest.Authentication, Manifest.Limits, action }, ConnectorPackageVerifier.JsonOptions)));
}

/// <summary>Verifies a descriptor release against deployment trust before any tenant import or capability registration.</summary>
internal sealed class ConnectorPackageVerifier(ConnectorPackageTrustOptions trust)
{
    internal const int HostContractVersion = 1;
    internal const int MaximumArtifactBytes = 1_048_576;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false, MaxDepth = 32,
    };

    public VerifiedConnectorPackage Verify(string artifactJson)
    {
        if (string.IsNullOrWhiteSpace(artifactJson) || Encoding.UTF8.GetByteCount(artifactJson) > MaximumArtifactBytes) throw Invalid("ArtifactSize");
        ConnectorPackageArtifact artifact;
        try { artifact = JsonSerializer.Deserialize<ConnectorPackageArtifact>(artifactJson, JsonOptions) ?? throw Invalid("ArtifactInvalid"); }
        catch (JsonException) { throw Invalid("ArtifactInvalid"); }
        if (artifact.SchemaVersion != 1 || !IsKey(artifact.PublisherId) || !IsKey(artifact.KeyId)
            || artifact.Sha256 is not { Length: 64 } || !artifact.Sha256.All(Uri.IsHexDigit)) throw Invalid("ArtifactInvalid");
        ConnectorPackagePublisher publisher = trust.TrustedPublishers.SingleOrDefault(publisher => publisher.PublisherId == artifact.PublisherId
            && publisher.KeyId == artifact.KeyId) ?? throw Invalid("PublisherNotTrusted");
        byte[] payload; byte[] signature; byte[] expected;
        try { payload = Convert.FromBase64String(artifact.PayloadBase64); signature = Convert.FromBase64String(artifact.SignatureBase64); expected = Convert.FromHexString(artifact.Sha256); }
        catch (Exception exception) when (exception is FormatException or ArgumentNullException) { throw Invalid("ArtifactEncoding"); }
        if (payload.Length is 0 or > 524_288 || signature.Length is < 256 or > 512) throw Invalid("ArtifactSize");
        byte[] digest = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(digest, expected)) throw Invalid("ArtifactDigestMismatch");
        try
        {
            using RSA key = RSA.Create(); key.ImportFromPem(publisher.PublicKeyPem);
            if (key.KeySize is < 2048 or > 4096 || !key.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw Invalid("ArtifactSignatureInvalid");
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException) { throw Invalid("ArtifactSignatureInvalid"); }
        ConnectorPackageManifest manifest;
        try { manifest = JsonSerializer.Deserialize<ConnectorPackageManifest>(payload, JsonOptions) ?? throw Invalid("ManifestInvalid"); }
        catch (JsonException) { throw Invalid("ManifestInvalid"); }
        Validate(manifest, artifact.PublisherId);
        return new(manifest, Convert.ToHexStringLower(digest), artifactJson);
    }

    private static void Validate(ConnectorPackageManifest manifest, string publisherId)
    {
        if (manifest.SchemaVersion != 1 || manifest.PublisherId != publisherId || !IsKey(manifest.PackageKey)
            || manifest.PackageVersion is null || !Regex.IsMatch(manifest.PackageVersion, "^[0-9]{1,5}\\.[0-9]{1,5}\\.[0-9]{1,5}$", RegexOptions.NonBacktracking)
            || !Version.TryParse(manifest.PackageVersion, out Version? version) || version.ToString(3) != manifest.PackageVersion
            || manifest.ConnectorKey is null || !Regex.IsMatch(manifest.ConnectorKey, "^[a-z][a-z0-9-]{0,79}$", RegexOptions.NonBacktracking)
            || manifest.DisplayName is not { Length: > 0 and <= 160 } || manifest.AdapterKind is not ("Native" or "Mcp" or "Http")
            || manifest.Authentication is null || manifest.Limits is null || manifest.Actions is null || manifest.Actions.Count is < 1 or > 100)
            throw Invalid("ManifestInvalid");
        if (manifest.MinimumHostContractVersion is < 1 or > HostContractVersion || manifest.MaximumHostContractVersion < HostContractVersion)
            throw Invalid("HostContractUnsupported");
        if (manifest.Authentication.Kind is not ("OAuth2" or "None" or "BearerToken" or "ApiKeyHeader" or "Basic")
            || (manifest.Authentication.Kind == "ApiKeyHeader" && (manifest.Authentication.HeaderName is null
                || !Regex.IsMatch(manifest.Authentication.HeaderName, "^X-[A-Za-z0-9-]{1,70}$", RegexOptions.NonBacktracking)))
            || (manifest.Authentication.Kind != "ApiKeyHeader" && manifest.Authentication.HeaderName is not null)) throw Invalid("AuthenticationUnsupported");
        if (manifest.AdapterKind != "Native" && manifest.Authentication.Kind == "OAuth2") throw Invalid("NativeOAuthAdapterRequired");
        if (manifest.Limits.MaximumInputBytes is < 1 or > 65_536 || manifest.Limits.MaximumOutputBytes is < 1 or > 65_536
            || manifest.Limits.TimeoutSeconds is < 1 or > 60) throw Invalid("ResourceLimitsInvalid");
        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConnectorPackageAction action in manifest.Actions)
        {
            if (action is null || action.Key is null || !action.Key.StartsWith(manifest.ConnectorKey + ".", StringComparison.Ordinal)
                || action.Key.Length > 128 || !Regex.IsMatch(action.Key, "^[a-z][a-z0-9_.-]+$", RegexOptions.NonBacktracking)
                || action.Version is < 1 or > 1_000_000 || !actions.Add(action.Key + "@" + action.Version)
                || action.DisplayName is not { Length: > 0 and <= 160 } || action.MinimumRiskTier is not ("ReadOnly" or "Low" or "Medium" or "High")
                || !IsObjectSchema(action.InputSchema) || action.OutputSchema.ValueKind != JsonValueKind.Object
                || Encoding.UTF8.GetByteCount(action.InputSchema.GetRawText()) > 65_536 || Encoding.UTF8.GetByteCount(action.OutputSchema.GetRawText()) > 65_536
                || action.RequiredScopes is null || action.RequiredScopes.Count > 20 || action.RequiredScopes.Any(scope => !IsScope(scope)))
                throw Invalid("ActionContractInvalid");
            if (action.ScopeAlternatives is { } alternatives && (alternatives.Count is < 1 or > 10
                || alternatives.Any(option => option is null || option.Count is < 1 or > 20 || option.Any(scope => !IsScope(scope)))))
                throw Invalid("ActionContractInvalid");
            if (manifest.AdapterKind != "Native" && (action.RequiredScopes.Count != 0 || action.ScopeAlternatives is not null)) throw Invalid("NativeOAuthAdapterRequired");
            if (!Uri.TryCreate(action.Endpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme is not ("https" or "http")
                || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0 || action.Endpoint.Length > 2048) throw Invalid("EndpointInvalid");
            if (manifest.AdapterKind == "Mcp" && (!IsToolName(action.RemoteToolName) || action.HttpMethod is not null)) throw Invalid("McpActionInvalid");
            if (manifest.AdapterKind == "Http" && (action.RemoteToolName is not null || action.HttpMethod is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))) throw Invalid("HttpActionInvalid");
            if (manifest.AdapterKind == "Native" && (action.RemoteToolName is not null || action.HttpMethod is not null)) throw Invalid("NativeActionInvalid");
        }
        if (manifest.AdapterKind == "Mcp" && manifest.Actions.Select(action => action.Endpoint).Distinct(StringComparer.Ordinal).Count() != 1)
            throw Invalid("McpEndpointMismatch");
    }

    private static bool IsKey(string? value) => value is { Length: > 0 and <= 100 }
        && Regex.IsMatch(value, "^[a-z][a-z0-9_.-]*$", RegexOptions.NonBacktracking);
    private static bool IsToolName(string? value) => value is { Length: > 0 and <= 100 }
        && Regex.IsMatch(value, "^[a-zA-Z_][a-zA-Z0-9_-]*$", RegexOptions.NonBacktracking);
    private static bool IsScope(string? value) => value is { Length: > 0 and <= 200 } && !value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character));
    private static bool IsObjectSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out JsonElement type)) return false;
        return type.ValueKind == JsonValueKind.String ? type.GetString() == "object"
            : type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == "object")
                && type.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && item.GetString() is "object" or "null");
    }
    private static InvalidOperationException Invalid(string code) => new("ConnectorPackage." + code);
}
