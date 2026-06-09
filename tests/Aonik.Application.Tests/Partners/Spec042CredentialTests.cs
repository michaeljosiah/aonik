using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Partners;
using Aonik.Finance.Services.Partners.Connectors;
using Aonik.Finance.Services.Partners.Connectors.Credentials;
using Aonik.Finance.Services.Partners.Connectors.Flutterwave;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Aonik.Application.Tests.Partners;

/// <summary>
/// Spec 042 acceptance tests — credential bundle encryption + isolation, fail-closed resolution, the webhook
/// signing-secret rotation window, and ConfigJson schema validation.
/// </summary>
public class Spec042CredentialTests
{
    private const string PayoutKind = "flutterwave-payout-v4";
    private const string BillsKind = "flutterwave-bills-v3";

    // ── ConfigJson schema validation (§10) ──────────────────────────────────────
    [Fact]
    public void ConnectorConfigJson_Should_Reject_UnknownKey()
    {
        var descriptor = ConnectorRegistry.GetRequired(PayoutKind);
        var act = () => ConnectorConfigJson.Validate(descriptor, """{"environment":"sandbox","wat":"x"}""");
        act.Should().Throw<InvalidOperationException>().WithMessage("*wat*");
    }

    [Fact]
    public void ConnectorConfigJson_Should_Reject_BadEnumValue()
    {
        var descriptor = ConnectorRegistry.GetRequired(PayoutKind);
        var act = () => ConnectorConfigJson.Validate(descriptor, """{"environment":"staging"}""");
        act.Should().Throw<InvalidOperationException>().WithMessage("*environment*");
    }

    [Fact]
    public void ConnectorConfigJson_Should_Reject_MissingRequired()
    {
        var descriptor = ConnectorRegistry.GetRequired(PayoutKind);
        var act = () => ConnectorConfigJson.Validate(descriptor, "{}");
        act.Should().Throw<InvalidOperationException>().WithMessage("*environment*");
    }

    [Fact]
    public void ConnectorConfigJson_Should_Accept_ValidConfig()
    {
        var descriptor = ConnectorRegistry.GetRequired(PayoutKind);
        var act = () => ConnectorConfigJson.Validate(descriptor, """{"environment":"production","defaultTransferPurpose":"x"}""");
        act.Should().NotThrow();
    }

    // ── Registry (§4) ───────────────────────────────────────────────────────────
    [Fact]
    public void ConnectorRegistry_Should_Map_Kinds_To_Provider_And_Port()
    {
        ConnectorRegistry.GetRequired(PayoutKind).Port.Should().Be(PartnerServiceCategory.Payout);
        ConnectorRegistry.GetRequired(BillsKind).Port.Should().Be(PartnerServiceCategory.BillPayment);
        ConnectorRegistry.GetRequired(PayoutKind).ProviderCode.Should().Be("Flutterwave");
        ConnectorRegistry.Get("nope").Should().BeNull();
        ConnectorRegistry.ForProvider("Flutterwave").Should().HaveCount(2);
    }

    // ── Rotation window, read-time expiry (§11) ─────────────────────────────────
    [Fact]
    public void CredentialSecretStore_Should_Verify_Previous_Only_Within_Window()
    {
        var rotatedAt = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
        var store = new CredentialSecretStore();
        store.Set("signingSecret", "old");
        store.Rotate("signingSecret", "new", rotatedAt.AddHours(24));

        var withinWindow = store.GetVerificationCandidates("signingSecret", rotatedAt.AddHours(1));
        withinWindow.Should().BeEquivalentTo(new[] { "new", "old" });

        var afterWindow = store.GetVerificationCandidates("signingSecret", rotatedAt.AddHours(25));
        afterWindow.Should().BeEquivalentTo(new[] { "new" });
    }

    // ── Bundle encryption at rest + value-free reads + isolation (§6, §16) ──────
    [Fact]
    public async Task Bundle_Should_Store_Secret_Encrypted_And_Round_Trip()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId, out var clock);
        var service = CreateBundleService(db, tenantId, clock);

        await service.UpsertAsync(new CredentialBundleWriteRequest(
            "fw-a", "Flutterwave A", PayoutKind,
            new Dictionary<string, string> { ["clientId"] = "id-A", ["clientSecret"] = "super-secret-A" }));

        // Encrypted at rest: the persisted ciphertext + the value-free metadata never contain the plaintext.
        var row = await db.CredentialBundles.AsNoTracking().SingleAsync();
        row.ProtectedSecretsJson.Should().NotBeNullOrWhiteSpace();
        row.ProtectedSecretsJson.Should().NotContain("super-secret-A");
        row.FieldMetadataJson.Should().NotContain("super-secret-A");

        // Server-side resolve round-trips the plaintext.
        var resolved = await service.ResolveAsync("fw-a");
        resolved!.Secrets.GetCurrent("clientSecret").Should().Be("super-secret-A");

        // Field state is value-free.
        var states = await service.GetFieldStatesAsync("fw-a");
        states.Should().Contain(s => s.Name == "clientSecret" && s.IsSet);
    }

    [Fact]
    public async Task Bundles_Should_Resolve_Independently()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId, out var clock);
        var service = CreateBundleService(db, tenantId, clock);

        await service.UpsertAsync(new CredentialBundleWriteRequest(
            "fw-uk", "UK", PayoutKind, new Dictionary<string, string> { ["clientId"] = "id-uk", ["clientSecret"] = "secret-uk" }));
        await service.UpsertAsync(new CredentialBundleWriteRequest(
            "fw-ng", "NG", PayoutKind, new Dictionary<string, string> { ["clientId"] = "id-ng", ["clientSecret"] = "secret-ng" }));

        (await service.ResolveAsync("fw-uk"))!.Secrets.GetCurrent("clientSecret").Should().Be("secret-uk");
        (await service.ResolveAsync("fw-ng"))!.Secrets.GetCurrent("clientSecret").Should().Be("secret-ng");
    }

    [Fact]
    public async Task Bundle_Rotation_Should_Keep_Previous_Within_Window()
    {
        var tenantId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb(tenantId, out var clock);
        clock.Setup(c => c.UtcNow).Returns(() => now);
        var service = CreateBundleService(db, tenantId, clock);

        await service.UpsertAsync(new CredentialBundleWriteRequest(
            "fw-r", "Rotate", PayoutKind,
            new Dictionary<string, string> { ["clientId"] = "id", ["clientSecret"] = "secret", ["signingSecret"] = "sig-old" }));

        (await service.RotateFieldAsync("fw-r", "signingSecret", "sig-new", TimeSpan.FromHours(24))).Should().BeTrue();

        var resolved = await service.ResolveAsync("fw-r");
        resolved!.Secrets.GetVerificationCandidates("signingSecret", now.AddHours(1)).Should().BeEquivalentTo(new[] { "sig-new", "sig-old" });
        resolved.Secrets.GetVerificationCandidates("signingSecret", now.AddHours(25)).Should().BeEquivalentTo(new[] { "sig-new" });
    }

    [Fact]
    public async Task Upsert_Should_Reject_Unknown_Credential_Field()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId, out var clock);
        var service = CreateBundleService(db, tenantId, clock);

        var act = async () => await service.UpsertAsync(new CredentialBundleWriteRequest(
            "fw-x", "X", PayoutKind, new Dictionary<string, string> { ["wat"] = "x" }));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Create must not silently update an existing ref (§6) ────────────────────
    [Fact]
    public async Task CreateBundle_Should_Reject_Existing_Ref()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId, out var clock);
        var admin = CreateAdminService(db, tenantId, clock);

        await admin.CreateBundleAsync(new CreateCredentialBundleRequest(
            "fw-dup", "First", PayoutKind, new Dictionary<string, string> { ["clientId"] = "id", ["clientSecret"] = "s1" }));

        // A second create with the same ref must FAIL rather than silently change kind/secrets out from under
        // connectors that already bind it.
        var act = async () => await admin.CreateBundleAsync(new CreateCredentialBundleRequest(
            "fw-dup", "Second", BillsKind, new Dictionary<string, string> { ["secretKey"] = "other" }));
        await act.Should().ThrowAsync<InvalidOperationException>();

        // The original bundle is untouched: its kind was NOT flipped to the bills kind by the rejected create.
        var row = await db.CredentialBundles.AsNoTracking().SingleAsync(b => b.Ref == "fw-dup");
        row.ConnectorKind.Should().Be(PayoutKind);
    }

    // ── Config provider fail-closed precedence (§7.2, §8) ───────────────────────
    [Fact]
    public async Task ConfigProvider_Should_Fail_Closed_When_No_Bundle_And_Not_Default()
    {
        var provider = CreateConfigProvider(out _);
        var binding = new ConnectorBinding(Guid.NewGuid(), PayoutKind, "Flutterwave", CredentialsRef: null, "{}", AllowLegacyFallback: false);

        var act = async () => await provider.GetAsync(binding);
        await act.Should().ThrowAsync<FlutterwaveException>();
    }

    [Fact]
    public async Task ConfigProvider_Should_Use_Legacy_For_Default_Connector()
    {
        var provider = CreateConfigProvider(out var settings);
        settings.Setup(s => s.GetAsync(PartnerGatewaySettingNames.FlutterwaveEnabled, It.IsAny<CancellationToken>())).ReturnsAsync("true");
        settings.Setup(s => s.GetAsync(PartnerGatewaySettingNames.FlutterwaveClientId, It.IsAny<CancellationToken>())).ReturnsAsync("legacy-id");
        settings.Setup(s => s.GetAsync(PartnerGatewaySettingNames.FlutterwaveClientSecret, It.IsAny<CancellationToken>())).ReturnsAsync("legacy-secret");
        settings.Setup(s => s.GetAsync(PartnerGatewaySettingNames.FlutterwaveBaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync("https://developersandbox-api.flutterwave.com");

        var binding = new ConnectorBinding(Guid.NewGuid(), PayoutKind, "Flutterwave", CredentialsRef: null, "{}", AllowLegacyFallback: true);
        var options = await provider.GetAsync(binding);

        options.IsConfigured().Should().BeTrue();
        options.ClientId.Should().Be("legacy-id");
    }

    [Fact]
    public async Task ConfigProvider_Should_Build_From_Bundle_With_Environment_Derived_Urls()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId, out var clock);
        var bundleService = CreateBundleService(db, tenantId, clock);
        await bundleService.UpsertAsync(new CredentialBundleWriteRequest(
            "fw-bnd", "Bound", PayoutKind,
            new Dictionary<string, string> { ["clientId"] = "bnd-id", ["clientSecret"] = "bnd-secret" }));

        var settings = new Mock<ISettingProvider>();
        var provider = new FlutterwaveConfigProvider(settings.Object, bundleService, Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions()));

        var binding = new ConnectorBinding(
            Guid.NewGuid(), PayoutKind, "Flutterwave", CredentialsRef: "fw-bnd",
            """{"environment":"production"}""", AllowLegacyFallback: false);
        var options = await provider.GetAsync(binding);

        options.ClientId.Should().Be("bnd-id");
        options.ClientSecret.Should().Be("bnd-secret");
        options.BaseUrl.Should().Be("https://f4bexperience.flutterwave.com"); // production environment → derived URL
        // No legacy setting was read — credentials came entirely from the bundle.
        settings.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    private static FinanceDbContext CreateDb(Guid tenantId, out Mock<IClock> clock)
    {
        clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(tenantId), currentUserProvider: null, clock.Object);
    }

    private static CredentialBundleService CreateBundleService(FinanceDbContext db, Guid tenantId, Mock<IClock> clock)
    {
        var protector = new ConnectorCredentialProtector(new EphemeralDataProtectionProvider());
        return new CredentialBundleService(db, protector, new TestTenantProvider(tenantId), Mock.Of<IAuditLogWriter>(), clock.Object);
    }

    private static CredentialBundleAdminService CreateAdminService(FinanceDbContext db, Guid tenantId, Mock<IClock> clock)
    {
        var bundleService = CreateBundleService(db, tenantId, clock);
        return new CredentialBundleAdminService(
            db, bundleService, new TestTenantProvider(tenantId), new Mock<ISettingProvider>().Object, clock.Object,
            new TestCurrentUserProvider(), new AllowAllPermissionService());
    }

    private static FlutterwaveConfigProvider CreateConfigProvider(out Mock<ISettingProvider> settings)
    {
        // ISettingProvider is public (Moq-proxyable); ICredentialBundleService is internal, so use the
        // hand-written Noop double (it resolves to "no bundle").
        settings = new Mock<ISettingProvider>();
        return new FlutterwaveConfigProvider(
            settings.Object,
            new Spec042NoopCredentialBundleService(),
            Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions()));
    }
}
