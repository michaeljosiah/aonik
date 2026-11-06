using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Azure.Core;
using FluentAssertions;

using Aonik.Infrastructure.Publishing;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Tests.Publishing;

public class SessionPackageVerifierTests
{
    private const string PoolId = "/subscriptions/336d38e2-bb96-4510-a4c5-5c313139eef9/resourceGroups/dev/providers/Microsoft.App/sessionPools/verifier";
    private static PackageVerifierOptions Policy() => new()
    {
        Provider = "AzureDynamicSessions", SessionPoolResourceId = PoolId, ReaderCommit = DockerPackageVerifier.ReaderPin,
        Image = "test.azurecr.io/verifier@sha256:" + new string('a', 64), MemoryMiB = 2048, ScratchMiB = 64, MaxAssetBytes = 16777216
    };

    private static JsonObject Profile() => new()
    {
        ["id"] = PoolId,
        ["identity"] = new JsonObject { ["type"] = "UserAssigned", ["userAssignedIdentities"] = new JsonObject { ["pull"] = new JsonObject() } },
        ["properties"] = new JsonObject
        {
            ["provisioningState"] = "Succeeded", ["containerType"] = "CustomContainer", ["poolManagementType"] = "Dynamic",
            ["poolManagementEndpoint"] = "https://verifier.test.uksouth.azurecontainerapps.io",
            ["templateUpdateStatus"] = new JsonObject { ["activeTemplate"] = new JsonObject { ["details"] = "Running",
                ["containers"] = new JsonArray(new JsonObject { ["image"] = Policy().Image }) } },
            ["managedIdentitySettings"] = new JsonArray(new JsonObject { ["identity"] = "pull", ["lifecycle"] = "None" }),
            ["sessionNetworkConfiguration"] = new JsonObject { ["status"] = "EgressDisabled" },
            ["scaleConfiguration"] = new JsonObject { ["maxConcurrentSessions"] = 2, ["readySessionInstances"] = 1 },
            ["dynamicPoolConfiguration"] = new JsonObject { ["lifecycleConfiguration"] = new JsonObject { ["lifecycleType"] = "OnContainerExit", ["maxAlivePeriodInSeconds"] = 300 } },
            ["customContainerTemplate"] = new JsonObject
            {
                ["registryCredentials"] = new JsonObject { ["identity"] = "pull" },
                ["ingress"] = new JsonObject { ["targetPort"] = 8080 },
                ["containers"] = new JsonArray(new JsonObject { ["image"] = Policy().Image, ["resources"] = new JsonObject { ["cpu"] = 1, ["memory"] = "2Gi" } })
            }
        }
    };

    [Theory]
    [InlineData("network")]
    [InlineData("image")]
    [InlineData("credentials")]
    [InlineData("command")]
    [InlineData("identity")]
    [InlineData("lifetime")]
    [InlineData("endpoint")]
    [InlineData("memory")]
    [InlineData("identity-mismatch")]
    [InlineData("old-runtime")]
    public async Task UnqualifiedPool_Should_RefuseBeforeUploading(string change)
    {
        using var h = new Harness();
        var p = h.Profile["properties"]!;
        var container = p["customContainerTemplate"]!["containers"]![0]!;
        switch (change)
        {
            case "network": p["sessionNetworkConfiguration"]!["status"] = "EgressEnabled"; break;
            case "image": container["image"] = "test.azurecr.io/verifier:latest"; break;
            case "credentials": container["env"] = new JsonArray(new JsonObject { ["name"] = "SECRET", ["value"] = "should-never-arrive" }); break;
            case "command": container["command"] = new JsonArray("sh"); break;
            case "identity": p["managedIdentitySettings"]![0]!["lifecycle"] = "All"; break;
            case "lifetime": p["dynamicPoolConfiguration"]!["lifecycleConfiguration"]!["maxAlivePeriodInSeconds"] = 3600; break;
            case "endpoint": p["poolManagementEndpoint"] = "https://attacker.example"; break;
            case "memory": container["resources"]!["memory"] = "16Gi"; break;
            case "identity-mismatch": p["managedIdentitySettings"]![0]!["identity"] = "other"; break;
            case "old-runtime": p["templateUpdateStatus"]!["activeTemplate"]!["containers"]![0]!["image"] = "old-image"; break;
        }
        (await h.Verify()).Passed.Should().BeFalse();
        h.SessionCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task QualifiedPool_Should_TransferExactBytesValidateReportAndDestroySession()
    {
        using var h = new Harness();
        var result = await h.Verify();
        result.Passed.Should().BeTrue();
        h.Uploaded.ToArray().Should().Equal(h.Bytes);
        h.SessionCalls.Should().EndWith("/.management/stopSession");
        h.ProfileReads.Should().Be(2);
        h.Identifiers.Distinct().Should().ContainSingle();
        string first = h.Identifiers[0];
        (await h.Verify()).Passed.Should().BeTrue();
        h.Identifiers.Last().Should().NotBe(first);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("hash")]
    [InlineData("fingerprint")]
    [InlineData("cleanup")]
    [InlineData("drift")]
    [InlineData("overflow")]
    [InlineData("ack")]
    public async Task UntrustedResponseOrUncertainCleanup_Should_HoldAndAttemptCleanup(string fault)
    {
        using var h = new Harness { Fault = fault };
        (await h.Verify()).Passed.Should().BeFalse();
        h.SessionCalls.Should().EndWith("/.management/stopSession");
    }

    [Fact]
    public async Task CallerCancellation_Should_StillDestroyAllocatedSession()
    {
        using var h = new Harness { Fault = "cancel" };
        using var cancellation = new CancellationTokenSource();
        h.Cancel = cancellation;
        await FluentActions.Awaiting(() => h.Verify(cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();
        h.SessionCalls.Should().EndWith("/.management/stopSession");
    }

    [Fact]
    public async Task AllocationCounters_Should_NotInvalidateAnUnchangedQualifiedProfile()
    {
        using var h = new Harness { Fault = "counters" };
        (await h.Verify()).Passed.Should().BeTrue();
    }

    [Fact]
    public async Task LargePackage_Should_UseBoundedChunksWithConfirmedOffsets()
    {
        using var h = new Harness(1048593);
        (await h.Verify()).Passed.Should().BeTrue();
        h.SessionCalls.Count(path => path == "/chunk").Should().Be(2);
        h.Uploaded.ToArray().Should().Equal(h.Bytes);
    }

    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("synthetic-test-token", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class Harness : HttpMessageHandler
    {
        public JsonObject Profile { get; } = SessionPackageVerifierTests.Profile();
        public byte[] Bytes { get; } = Encoding.UTF8.GetBytes("a synthetic package for transport testing");
        public MemoryStream Uploaded { get; } = new();
        public List<string> SessionCalls { get; } = [];
        public List<string> Identifiers { get; } = [];
        public int ProfileReads { get; private set; }
        public string? Fault { get; init; }
        public CancellationTokenSource? Cancel { get; set; }
        private readonly string _path = Path.GetTempFileName();
        private readonly HttpClient _http;
        private long _offset;
        private string _nonce = "";

        public Harness(int byteCount = 0)
        {
            if (byteCount > 0) Bytes = new byte[byteCount];
            File.WriteAllBytes(_path, Bytes);
            _http = new HttpClient(this, false);
        }

        public Task<PackageVerificationResult> Verify(CancellationToken ct = default)
            => new SessionPackageVerifier(_http, new Credential()).VerifyAsync(new VerifiedInput(_path, Hash(Bytes), Bytes.Length), Policy(), ct);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            request.Headers.Authorization!.Parameter.Should().Be("synthetic-test-token");
            if (request.RequestUri!.Host == "management.azure.com")
            {
                ProfileReads++;
                if (Fault == "drift" && ProfileReads == 2) Profile["properties"]!["sessionNetworkConfiguration"]!["status"] = "EgressEnabled";
                if (Fault == "counters") Profile["properties"]!["nodeCount"] = ProfileReads;
                return Ok(Profile);
            }
            string path = request.RequestUri.AbsolutePath;
            SessionCalls.Add(path);
            Identifiers.Add(request.RequestUri.Query.Split("identifier=")[1]);
            if (path == "/.management/stopSession") return new(Fault == "cleanup" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
            if (path == "/begin")
            {
                request.Content!.Headers.ContentLength.Should().BePositive("the bounded HTTP server rejects chunked declarations");
                var declaration = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
                _nonce = declaration["nonce"]!.GetValue<string>(); _offset = 0;
                return Ok(new { nonce = _nonce, offset = 0 });
            }
            request.Headers.GetValues("X-Verification-Nonce").Single().Should().Be(_nonce);
            if (path == "/chunk")
            {
                if (Fault == "cancel") { Cancel!.Cancel(); ct.ThrowIfCancellationRequested(); }
                request.Headers.GetValues("X-Upload-Offset").Single().Should().Be(_offset.ToString());
                var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
                bytes.Length.Should().BeLessThanOrEqualTo(1048576);
                Uploaded.Write(bytes); _offset += bytes.Length;
                return Ok(new { nonce = _nonce, offset = Fault == "ack" ? _offset - 1 : _offset });
            }
            if (Fault == "overflow") return new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 300000)) };
            return Ok(new { nonce = Fault == "nonce" ? "wrong" : _nonce, result = new PackageVerificationResult(true, "verified", DockerPackageVerifier.ReaderPin,
                Fault == "hash" ? new string('b', 64) : Hash(Bytes), Bytes.Length, "synthetic", "edition", Fault == "fingerprint" ? new string('a', 64) : Hash(Encoding.UTF8.GetBytes("edition")), "story", null, null, null, []) });
        }

        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        private static HttpResponseMessage Ok(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _http.Dispose(); Uploaded.Dispose(); File.Delete(_path); }
            base.Dispose(disposing);
        }
    }
}
