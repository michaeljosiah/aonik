using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Azure.Core;
using Azure.Identity;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

/// <summary>Single-use, network-isolated verification. The sandbox receives bytes, never storage access.</summary>
public sealed class SessionPackageVerifier(HttpClient http, TokenCredential credential) : IPackageVerifier
{
    internal const string ApiVersion = "2025-02-02-preview";
    internal const long MaxPackageBytes = 32L * 1024 * 1024;

    public async Task<PackageVerificationResult> VerifyAsync(VerifiedInput input, PackageVerifierOptions policy, CancellationToken cancellationToken = default)
    {
        if (!Qualified(policy) || !Path.IsPathFullyQualified(input.Path) || input.Bytes is < 1 or > MaxPackageBytes
            || !Regex.IsMatch(input.Sha256, "^[a-f0-9]{64}$"))
            return VerifierProtocol.Refused("verifier-unavailable", policy);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(180));
        var ct = limit.Token;
        string nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        string session = "verify-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        Uri? endpoint = null;
        bool allocated = false;
        string stage = "pool";
        var result = VerifierProtocol.Refused("verifier-unavailable", policy);
        try
        {
            var profile = await GetProfileAsync(policy, ct);
            endpoint = profile.Endpoint;
            allocated = true; // Even an uncertain response may have allocated the sandbox.
            stage = "begin";
            using var begin = await SendSessionAsync(endpoint, session, "begin", nonce, new StringContent(JsonSerializer.Serialize(new
            {
                nonce, sha256 = input.Sha256, bytes = input.Bytes, maxAssets = policy.MaxAssets,
                maxAssetBytes = policy.MaxAssetBytes, maxDocumentBytes = policy.MaxDocumentBytes, timeoutSeconds = policy.TimeoutSeconds
            }), Encoding.UTF8, "application/json"), null, ct);
            CheckAck(begin.RootElement, nonce, 0);

            await using var file = new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (file.Length != input.Bytes) throw new InvalidDataException("Archive identity changed.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1048576];
            long offset = 0;
            int read;
            stage = "upload";
            while ((read = await file.ReadAsync(buffer, ct)) != 0)
            {
                if (offset + read > input.Bytes) throw new InvalidDataException("Archive identity changed.");
                hash.AppendData(buffer, 0, read);
                using var chunk = new ByteArrayContent(buffer, 0, read);
                chunk.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                using var ack = await SendSessionAsync(endpoint, session, "chunk", nonce, chunk, offset, ct);
                offset += read;
                CheckAck(ack.RootElement, nonce, offset);
            }
            if (offset != input.Bytes || Convert.ToHexStringLower(hash.GetHashAndReset()) != input.Sha256)
                throw new InvalidDataException("Archive identity changed.");
            stage = "verify";
            using var report = await SendSessionAsync(endpoint, session, "verify", nonce, new ByteArrayContent([]), null, ct);
            if (report.RootElement.GetProperty("nonce").GetString() != nonce) throw new InvalidDataException("Incorrect job provenance.");
            result = VerifierProtocol.Parse(report.RootElement.GetProperty("result").GetRawText(), input, policy);
            // Deployment drift invalidates even a plausible report; policy is checked at both boundaries.
            stage = "readback";
            var current = await GetProfileAsync(policy, ct);
            if (current != profile) result = VerifierProtocol.Refused("verifier-policy-changed", policy);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { result = VerifierProtocol.Refused("verifier-timeout", policy); }
        catch (HttpRequestException ex)
        { result = VerifierProtocol.Refused($"verifier-{stage}-http-{(ex.StatusCode is null ? "unavailable" : ((int)ex.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture))}", policy); }
        catch (Exception ex) when (ex is AuthenticationFailedException or Azure.RequestFailedException)
        { result = VerifierProtocol.Refused("verifier-auth-unavailable", policy); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException)
        { result = VerifierProtocol.Refused($"verifier-{stage}-invalid", policy); }
        finally
        {
            if (allocated && endpoint is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    using var request = await RequestAsync(HttpMethod.Post, SessionUri(endpoint, session, ".management/stopSession"), "https://dynamicsessions.io/.default", cleanup.Token);
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cleanup.Token);
                    if (!response.IsSuccessStatusCode) result = VerifierProtocol.Refused("verifier-cleanup-failed", policy);
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or AuthenticationFailedException or Azure.RequestFailedException)
                { result = VerifierProtocol.Refused("verifier-cleanup-failed", policy); }
            }
        }
        return result;
    }

    private async Task<PoolProfile> GetProfileAsync(PackageVerifierOptions policy, CancellationToken ct)
    {
        var uri = new Uri($"https://management.azure.com{policy.SessionPoolResourceId}?api-version={ApiVersion}");
        using var request = await RequestAsync(HttpMethod.Get, uri, "https://management.azure.com/.default", ct);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var json = await ReadBoundedAsync(response, 65536, ct);
        return ValidateProfile(json.RootElement, policy);
    }

    internal static PoolProfile ValidateProfile(JsonElement resource, PackageVerifierOptions policy)
    {
        var p = resource.GetProperty("properties");
        var template = p.GetProperty("customContainerTemplate");
        var containers = template.GetProperty("containers");
        var lifecycle = p.GetProperty("dynamicPoolConfiguration").GetProperty("lifecycleConfiguration");
        var identities = p.GetProperty("managedIdentitySettings");
        var assigned = resource.GetProperty("identity").GetProperty("userAssignedIdentities");
        var scale = p.GetProperty("scaleConfiguration");
        var active = p.GetProperty("templateUpdateStatus").GetProperty("activeTemplate");
        if (!string.Equals(resource.GetProperty("id").GetString(), policy.SessionPoolResourceId, StringComparison.OrdinalIgnoreCase)
            || p.GetProperty("provisioningState").GetString() != "Succeeded"
            || p.GetProperty("containerType").GetString() != "CustomContainer" || p.GetProperty("poolManagementType").GetString() != "Dynamic"
            || p.GetProperty("sessionNetworkConfiguration").GetProperty("status").GetString() != "EgressDisabled"
            || lifecycle.GetProperty("lifecycleType").GetString() != "OnContainerExit" || lifecycle.GetProperty("maxAlivePeriodInSeconds").GetInt32() != 300
            || scale.GetProperty("maxConcurrentSessions").GetInt32() != 2 || scale.GetProperty("readySessionInstances").GetInt32() != 1
            || containers.GetArrayLength() != 1 || identities.GetArrayLength() != 1
            || active.GetProperty("details").GetString() != "Running" || active.GetProperty("containers").GetArrayLength() != 1
            || active.GetProperty("containers")[0].GetProperty("image").GetString() != policy.Image
            || identities[0].GetProperty("lifecycle").GetString() != "None"
            || resource.GetProperty("identity").GetProperty("type").GetString() != "UserAssigned"
            || assigned.EnumerateObject().Count() != 1
            || !string.Equals(identities[0].GetProperty("identity").GetString(), assigned.EnumerateObject().Single().Name, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(template.GetProperty("registryCredentials").GetProperty("identity").GetString(), assigned.EnumerateObject().Single().Name, StringComparison.OrdinalIgnoreCase)
            || !Empty(p, "secrets") || !Empty(template, "volumes") || template.GetProperty("ingress").GetProperty("targetPort").GetInt32() != 8080)
            throw new InvalidDataException("Unqualified verification pool.");
        var container = containers[0];
        if (container.GetProperty("image").GetString() != policy.Image || !Empty(container, "env") || !Empty(container, "command")
            || !Empty(container, "args") || !Empty(container, "volumeMounts") || !Empty(container, "probes")
            || container.GetProperty("resources").GetProperty("cpu").GetDouble() != 1
            || container.GetProperty("resources").GetProperty("memory").GetString() != "2Gi")
            throw new InvalidDataException("Unqualified verification container.");
        var endpoint = new Uri(p.GetProperty("poolManagementEndpoint").GetString()!);
        string name = policy.SessionPoolResourceId.Split('/')[^1];
        if (endpoint.Scheme != "https" || !endpoint.IsDefaultPort || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0
            || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/"
            || !endpoint.Host.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase)
            || !endpoint.Host.EndsWith(".azurecontainerapps.io", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unqualified verification endpoint.");
        // Allocation counters change during normal execution; compare only the qualified configuration.
        return new PoolProfile(endpoint, string.Join('\n', template.GetRawText(), lifecycle.GetRawText(),
            identities.GetRawText(), resource.GetProperty("identity").GetRawText(), scale.GetRawText()));
    }

    private static bool Empty(JsonElement element, string name)
        => !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null
            || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0);

    internal static bool Qualified(PackageVerifierOptions policy)
        => policy.Provider == "AzureDynamicSessions" && policy.ReaderCommit == DockerPackageVerifier.ReaderPin
            && Regex.IsMatch(policy.Image, "^[a-z0-9.-]+[.]azurecr[.]io/[a-z0-9./_-]+@sha256:[a-f0-9]{64}$")
            && Regex.IsMatch(policy.SessionPoolResourceId, "^/subscriptions/[a-fA-F0-9-]{36}/resourceGroups/[a-zA-Z0-9_.()-]+/providers/Microsoft[.]App/sessionPools/[a-z0-9-]+$")
            && policy.MemoryMiB == 2048 && policy.ScratchMiB == 64 && policy.TimeoutSeconds is >= 1 and <= 45
            && policy.MaxAssets is >= 1 and <= 1024 && policy.MaxAssetBytes is >= 1 and <= 16777216
            && policy.MaxDocumentBytes is >= 1 and <= 4194304;

    private async Task<JsonDocument> SendSessionAsync(Uri endpoint, string session, string path, string nonce, HttpContent body, long? offset, CancellationToken ct)
    {
        using var request = await RequestAsync(HttpMethod.Post, SessionUri(endpoint, session, path), "https://dynamicsessions.io/.default", ct);
        request.Headers.Add("X-Verification-Nonce", nonce);
        if (offset.HasValue) request.Headers.Add("X-Upload-Offset", offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Content = body;
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await ReadBoundedAsync(response, path == "verify" ? 270336 : 4096, ct);
    }

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, Uri uri, string scope, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), ct);
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return request;
    }

    private static Uri SessionUri(Uri endpoint, string session, string path)
        => new(endpoint, $"{path}?api-version={ApiVersion}&identifier={session}");

    private static void CheckAck(JsonElement ack, string nonce, long offset)
    {
        if (ack.GetProperty("nonce").GetString() != nonce || ack.GetProperty("offset").GetInt64() != offset)
            throw new InvalidDataException("Incorrect upload acknowledgement.");
    }

    private static async Task<JsonDocument> ReadBoundedAsync(HttpResponseMessage response, int maximum, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(bytes, ct)) != 0)
        {
            if (buffer.Length + read > maximum) throw new InvalidDataException("Verifier response exceeds its bound.");
            buffer.Write(bytes, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }

    internal sealed record PoolProfile(Uri Endpoint, string Configuration);
}
