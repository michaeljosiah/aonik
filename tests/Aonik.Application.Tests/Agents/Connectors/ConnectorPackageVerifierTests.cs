using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Moq;

using Aonik.Agents.Services.Connectors.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Serialization;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Agents.Connectors;

public sealed class ConnectorPackageVerifierTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("inactive")]
    [InlineData("permission")]
    [InlineData("anonymous")]
    public async Task Discovery_Should_ReturnOnlyVerifiedDeploymentArtifacts_ToAnActiveAuthorizedTenantReader(string denied)
    {
        using var signer = new Signer(); using var files = new Artifacts(signer, Manifest());
        var catalog = new CuratedConnectorCatalog(files.Options); Guid tenantId = Guid.NewGuid(); Guid userId = Guid.NewGuid();
        var current = new Mock<ICurrentUserProvider>(); current.Setup(value => value.GetCurrentUserId()).Returns(denied == "anonymous" ? null : userId);
        var permission = new Mock<IPermissionService>(); permission.Setup(value => value.HasPermissionAsync(userId, "Integration.Read", It.IsAny<CancellationToken>())).ReturnsAsync(denied != "permission");
        var users = new Mock<IUserDirectoryReader>(); users.Setup(value => value.GetByIdsAsync(tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserDirectoryItem(userId, null, denied == "inactive" ? "Inactive" : "Active")]);
        var service = new ConnectorPackageCatalogService(catalog, new TestTenantProvider(tenantId), current.Object, users.Object, permission.Object);
        if (denied != "none")
        {
            Func<Task> read = () => service.ListAsync();
            await read.Should().ThrowAsync<PermissionDeniedException>();
            return;
        }
        var listed = await service.ListAsync(); listed.Should().ContainSingle(); listed[0].Digest.Should().Be(catalog.Packages[0].Digest);
        (await service.GetAsync("synthetic-status", "1.0.0"))!.Manifest.Actions.Should().ContainSingle();
        (await service.DownloadAsync("synthetic-status", "1.0.0")).Should().Be(catalog.Packages[0].ArtifactJson);
        (await service.GetAsync("not-present", "1.0.0")).Should().BeNull();
    }

    [Fact]
    public void Catalog_Should_RetainExactOldReleasePins_AndRejectSchemaChangesUnderTheSameActionVersion()
    {
        using var signer = new Signer(); var first = Manifest();
        using var files = new Artifacts(signer, first, first with { PackageVersion = "1.1.0" });
        var catalog = new CuratedConnectorCatalog(files.Options);
        var old = catalog.Require(first.PackageKey, "1.0.0");
        catalog.Packages.Select(package => package.Manifest.PackageVersion).Should().Equal("1.1.0", "1.0.0");
        catalog.Require(old.Pin(old.Manifest.Actions[0])).Digest.Should().Be(old.Digest);
        Action changedDigest = () => catalog.Require(old.Pin(old.Manifest.Actions[0]) with { ArtifactDigest = new string('a', 64) });
        changedDigest.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.PackageDigestChanged");

        var changed = first with { PackageVersion = "2.0.0", Actions = [first.Actions[0] with
            { InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { other = new { type = "string" } } }) }] };
        using var drift = new Artifacts(signer, first, changed);
        Action load = () => new CuratedConnectorCatalog(drift.Options);
        load.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ActionContractDrift");
    }

    [Fact]
    public void Catalog_Should_RejectCredentialBehaviorChangesWithoutAnActionVersionChange_AndConfineArtifactPaths()
    {
        using var signer = new Signer(); var first = Manifest();
        using var changed = new Artifacts(signer, first, first with { PackageVersion = "2.0.0", Authentication = new("BearerToken") });
        Action drift = () => new CuratedConnectorCatalog(changed.Options);
        drift.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ActionContractDrift");
        changed.Options.ArtifactFiles = ["../outside.json"];
        Action traversal = () => new CuratedConnectorCatalog(changed.Options);
        traversal.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ArtifactPathInvalid");
    }

    [Fact]
    public void NativeGmailPackage_Should_MatchDeployedContracts_AndRefuseASignedRiskDowngrade()
    {
        using var signer = new Signer(); var registration = new GmailConnectorDescriptor();
        ConnectorPackageManifest native = NativeGmailManifest();
        VerifiedConnectorPackage valid = signer.Verifier.Verify(signer.Sign(native));
        CuratedConnectorCatalog.RequireNativeContracts(valid, [registration]);
        native.Actions.Should().HaveCount(19);
        var downgraded = native with { Actions = native.Actions.Select(action => action.Key == GmailActionKeys.SendMessage
            ? action with { MinimumRiskTier = "ReadOnly" } : action).ToArray() };
        VerifiedConnectorPackage signedDowngrade = signer.Verifier.Verify(signer.Sign(downgraded));

        Action check = () => CuratedConnectorCatalog.RequireNativeContracts(signedDowngrade, [registration]);

        check.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.NativeContractMismatch");
    }

    [Fact]
    public void Verify_Should_AcceptAnAuthenticBoundedDescriptor_AndRetainTheExactDownloadedArtifact()
    {
        using var signer = new Signer(); ConnectorPackageManifest manifest = Manifest();
        string artifact = signer.Sign(manifest);

        VerifiedConnectorPackage verified = signer.Verifier.Verify(artifact);

        verified.Manifest.PackageKey.Should().Be(manifest.PackageKey); verified.ArtifactJson.Should().Be(artifact);
        verified.Pin(verified.Manifest.Actions[0]).ArtifactDigest.Should().Be(verified.Digest);
        verified.Pin(verified.Manifest.Actions[0]).ActionContractHash.Should().HaveLength(64);
    }

    [Theory]
    [InlineData("payload", "ArtifactDigestMismatch")]
    [InlineData("signature", "ArtifactSignatureInvalid")]
    [InlineData("publisher", "PublisherNotTrusted")]
    [InlineData("key", "PublisherNotTrusted")]
    public void Verify_Should_RefuseTamperedOrUntrustedArtifacts_BeforeImport(string change, string code)
    {
        using var signer = new Signer(); string json = signer.Sign(Manifest());
        var artifact = JsonSerializer.Deserialize<ConnectorPackageArtifact>(json, ConnectorPackageVerifier.JsonOptions)!;
        artifact = change switch
        {
            "payload" => artifact with { PayloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")) },
            "signature" => artifact with { SignatureBase64 = Convert.ToBase64String(new byte[256]) },
            "publisher" => artifact with { PublisherId = "untrusted" },
            _ => artifact with { KeyId = "other-key" },
        };

        Action verify = () => signer.Verifier.Verify(JsonSerializer.Serialize(artifact, ConnectorPackageVerifier.JsonOptions));

        verify.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage." + code);
    }

    [Fact]
    public void Verify_Should_RefuseARehashedRiskDowngrade_WithoutAPublisherSignature()
    {
        using var signer = new Signer();
        var original = JsonSerializer.Deserialize<ConnectorPackageArtifact>(signer.Sign(Manifest()), ConnectorPackageVerifier.JsonOptions)!;
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(original.PayloadBase64)).Replace("ReadOnly", "Low", StringComparison.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        var changed = original with { PayloadBase64 = Convert.ToBase64String(bytes), Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };

        Action verify = () => signer.Verifier.Verify(JsonSerializer.Serialize(changed, ConnectorPackageVerifier.JsonOptions));

        verify.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ArtifactSignatureInvalid");
    }

    [Theory]
    [InlineData("adapter", "ManifestInvalid")]
    [InlineData("host", "HostContractUnsupported")]
    [InlineData("oauth", "NativeOAuthAdapterRequired")]
    [InlineData("scopes", "NativeOAuthAdapterRequired")]
    [InlineData("output", "ResourceLimitsInvalid")]
    [InlineData("endpoint", "EndpointInvalid")]
    [InlineData("userinfo", "EndpointInvalid")]
    [InlineData("duplicate", "ActionContractInvalid")]
    [InlineData("version", "ActionContractInvalid")]
    public void Verify_Should_RejectUnsupportedOrUnsafeSignedContracts(string change, string code)
    {
        using var signer = new Signer(); ConnectorPackageManifest manifest = Manifest();
        ConnectorPackageAction action = manifest.Actions[0];
        manifest = change switch
        {
            "adapter" => manifest with { AdapterKind = "Dll" },
            "host" => manifest with { MinimumHostContractVersion = 2, MaximumHostContractVersion = 3 },
            "oauth" => manifest with { Authentication = new("OAuth2") },
            "scopes" => manifest with { Actions = [action with { RequiredScopes = ["read"] }] },
            "output" => manifest with { Limits = manifest.Limits with { MaximumOutputBytes = 1_000_000 } },
            "endpoint" => manifest with { Actions = [action with { Endpoint = "file:///private/config" }] },
            "userinfo" => manifest with { Actions = [action with { Endpoint = "https://user:secret@example.test/records" }] },
            "duplicate" => manifest with { Actions = [action, action] },
            _ => manifest with { Actions = [action with { Version = 0 }] },
        };

        Action verify = () => signer.Verifier.Verify(signer.Sign(manifest));

        verify.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage." + code);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("alias")]
    [InlineData("script")]
    public void Verify_Should_RejectAmbiguousOrExecutablePayloadFields_EvenFromATrustedPublisher(string change)
    {
        using var signer = new Signer(); string payload = JsonSerializer.Serialize(Manifest(), ConnectorPackageVerifier.JsonOptions);
        payload = change switch
        {
            "duplicate" => payload.Insert(1, "\"schemaVersion\":1,"),
            "alias" => payload.Replace("\"schemaVersion\"", "\"SchemaVersion\"", StringComparison.Ordinal),
            _ => payload.Insert(1, "\"installationScript\":\"do-not-run\","),
        };

        Action verify = () => signer.Verifier.Verify(signer.SignPayload(payload));

        verify.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ManifestInvalid");
    }

    [Fact]
    public void ContractFingerprint_Should_PreserveLiteralCasingAndArrayOrder_WhileIgnoringObjectKeyOrder()
    {
        using JsonDocument first = JsonDocument.Parse("""{"b":{"Currency":"USD"},"a":[1,2]}""");
        using JsonDocument reordered = JsonDocument.Parse("""{"a":[1,2],"b":{"Currency":"USD"}}""");
        using JsonDocument changed = JsonDocument.Parse("""{"a":[2,1],"b":{"Currency":"usd"}}""");

        CanonicalJson.Hash(first.RootElement).Should().Be(CanonicalJson.Hash(reordered.RootElement));
        CanonicalJson.Hash(first.RootElement).Should().NotBe(CanonicalJson.Hash(changed.RootElement));
    }

    internal static ConnectorPackageManifest Manifest() => new(1, "synthetic-publisher", "synthetic-status", "1.0.0", "synthetic", "Synthetic status",
        "Http", 1, 1, new("None"), new(), [new("synthetic.records.get", 1, "Get record", "ReadOnly",
            JsonSerializer.SerializeToElement(new { type = "object", properties = new { id = new { type = "string" } }, required = new[] { "id" }, additionalProperties = false }),
            JsonSerializer.SerializeToElement(new { type = "object", properties = new { status = new { type = "string" } }, additionalProperties = false }), [],
            "https://example.test/records/{id}", HttpMethod: "GET")]);

    [Fact]
    public void Catalog_Should_RejectAReadAliasForAnIndependentlyClassifiedRemoteWrite()
    {
        using var signer = new Signer();
        ConnectorPackageManifest template = Manifest();
        ConnectorPackageAction read = template.Actions[0] with { Endpoint = "https://example.test/mcp", RemoteToolName = "change_record", HttpMethod = null };
        ConnectorPackageAction write = read with { Key = "synthetic.records.change", MinimumRiskTier = "High" };
        using var files = new Artifacts(signer, template with { AdapterKind = "Mcp", Actions = [read, write] });

        Action load = () => new CuratedConnectorCatalog(files.Options);

        load.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ConflictingEndpointRisk");
    }

    internal static ConnectorPackageManifest NativeGmailManifest() => Manifest() with
    {
        PackageKey = "native-gmail",
        ConnectorKey = GmailActionKeys.Connector,
        AdapterKind = "Native",
        Authentication = new("OAuth2"),
        Actions = new GmailConnectorDescriptor().Descriptor.Actions.Select(action =>
            new ConnectorPackageAction(action.Key, action.Version, action.ToolName,
                action.Approval?.Tier.ToString() ?? "ReadOnly", action.InputSchema, action.OutputSchema, action.RequiredScopes,
                "https://gmail.googleapis.com/gmail/v1/users/me/", ScopeAlternatives: action.ScopeAlternatives)).ToArray()
    };

    internal sealed class Signer : IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);
        public ConnectorPackageTrustOptions Trust => new() { TrustedPublishers = [new("synthetic-publisher", "test-key", _key.ExportSubjectPublicKeyInfoPem())] };
        public ConnectorPackageVerifier Verifier => new(Trust);
        public string Sign(ConnectorPackageManifest manifest) => SignPayload(JsonSerializer.Serialize(manifest, ConnectorPackageVerifier.JsonOptions));
        public string SignPayload(string payload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            var artifact = new ConnectorPackageArtifact(1, "synthetic-publisher", "test-key", Convert.ToBase64String(bytes), Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Convert.ToBase64String(_key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
            return JsonSerializer.Serialize(artifact, ConnectorPackageVerifier.JsonOptions);
        }
        public void Dispose() => _key.Dispose();
    }

    internal sealed class Artifacts : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "aonik-catalog-tests-" + Guid.NewGuid().ToString("N"));
        public ConnectorPackageTrustOptions Options { get; }
        public Artifacts(Signer signer, params ConnectorPackageManifest[] manifests)
        {
            Directory.CreateDirectory(_directory); Options = signer.Trust; Options.ArtifactDirectory = _directory;
            foreach (var item in manifests.Select((manifest, index) => (manifest, index)))
            {
                string file = "package-" + item.index + ".json";
                File.WriteAllText(Path.Combine(_directory, file), signer.Sign(item.manifest), new UTF8Encoding(false)); Options.ArtifactFiles.Add(file);
            }
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
