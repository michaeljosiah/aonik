using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Partners.Connectors.Credentials;
using Aonik.Finance.Services.Partners.Connectors.Registry;
using Aonik.Finance.Services.Payments;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Tests.ExternalServices;

public class StripeConnectorResolverTests
{
    [Fact]
    public async Task Selected_Should_UseOnlyExplicitTenantConnectorAndProtectedBundle()
    {
        using var test = new Harness();
        var connector = await test.SeedAsync();
        test.Settings.Setup(s => s.GetTenantValueAsync(StripeSettingNames.ConnectorId, test.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connector.Id.ToString());

        var result = await test.Resolver.ResolveSelectedAsync();

        result.ConnectorId.Should().Be(connector.Id);
        result.TenantId.Should().Be(test.TenantId);
        result.SecretKey.Should().Be("sk_test_fixture");
        result.ReturnOrigin.Should().Be("https://shop.example");
        result.ProviderAccountId.Should().Be("acct_merchant");
        result.LiveMode.Should().BeFalse();
        test.Settings.Verify(s => s.GetTenantValueAsync(StripeSettingNames.ConnectorId, test.TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Selected_Should_NotChooseAnArbitraryConnectorWhenSettingIsMissing()
    {
        using var test = new Harness();
        await test.SeedAsync();

        var act = () => test.Resolver.ResolveSelectedAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
    }

    [Fact]
    public async Task Bound_Should_AllowReconciliationOfDisabledConnectorWhileSelectedRejectsIt()
    {
        using var test = new Harness();
        var connector = await test.SeedAsync();
        connector.Status = "Disabled";
        await test.Db.SaveChangesAsync();
        test.Settings.Setup(s => s.GetTenantValueAsync(StripeSettingNames.ConnectorId, test.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connector.Id.ToString());

        var selected = () => test.Resolver.ResolveSelectedAsync();
        await selected.Should().ThrowAsync<InvalidOperationException>();
        (await test.Resolver.ResolveBoundAsync(connector.Id)).ConnectorId.Should().Be(connector.Id);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("deleted")]
    [InlineData("kind")]
    [InlineData("missing-bundle")]
    [InlineData("bundle-kind")]
    [InlineData("live-key")]
    public async Task Bound_Should_RejectCrossTenantMissingAndMismatchedBindings(string fault)
    {
        using var test = new Harness();
        var connector = await test.SeedAsync();
        if (fault == "deleted") connector.IsDeleted = true;
        if (fault == "kind") connector.ConnectorType = ConnectorRegistry.FlutterwaveBillsV3;
        if (fault == "missing-bundle") connector.CredentialsRef = "not-in-this-tenant";
        if (fault == "bundle-kind") (await test.Db.CredentialBundles.SingleAsync()).ConnectorKind = ConnectorRegistry.FlutterwaveBillsV3;
        if (fault == "live-key") await test.Bundles.RotateFieldAsync("stripe", "secretKey", "sk_live_wrong");
        await test.Db.SaveChangesAsync();

        var resolver = fault == "tenant"
            ? new StripeConnectorResolver(test.Db, new FixedTenantProvider(Guid.NewGuid()), test.Settings.Object, test.Bundles, test.Clock.Object)
            : test.Resolver;
        var act = () => resolver.ResolveBoundAsync(connector.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Bound_Should_IncludePreviousSigningSecretOnlyDuringRotationGrace()
    {
        using var test = new Harness();
        var connector = await test.SeedAsync();
        await test.Bundles.RotateFieldAsync("stripe", "signingSecret", "whsec_new", TimeSpan.FromHours(1));

        (await test.Resolver.ResolveBoundAsync(connector.Id)).SigningSecrets.Should().Equal("whsec_new", "whsec_old");
        test.Clock.SetupGet(c => c.UtcNow).Returns(test.Now.AddHours(2));
        (await test.Resolver.ResolveBoundAsync(connector.Id)).SigningSecrets.Should().Equal("whsec_new");
    }

    [Theory]
    [InlineData("http://shop.example", "acct_merchant")]
    [InlineData("https://shop.example/path", "acct_merchant")]
    [InlineData("https://user@shop.example", "acct_merchant")]
    [InlineData("https://shop.example?query=yes", "acct_merchant")]
    [InlineData("https://shop.example", "acct_bad/path")]
    public void Config_Should_RejectUnsafeOriginOrInvalidMerchant(string origin, string account)
    {
        var json = Config(origin: origin, account: account);

        var act = () => ConnectorConfigJson.Validate(ConnectorRegistry.GetRequired(ConnectorRegistry.StripeCheckoutV1), json);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("stripe-checkout-v1", "acct_other", "sandbox")]
    [InlineData("stripe-checkout-v1", "acct_merchant", "production")]
    [InlineData("flutterwave-bills-v3", "acct_merchant", "sandbox")]
    public void ConfigUpdate_Should_PreserveMerchantKindAndEnvironment(string kind, string account, string environment)
    {
        var act = () => ConnectorConfigJson.ValidateUpdate(ConnectorRegistry.StripeCheckoutV1, Config(),
            kind, Config(account: account, environment: environment));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConfigUpdate_Should_AllowChangingReturnOriginWithinSameMerchant()
    {
        var act = () => ConnectorConfigJson.ValidateUpdate(ConnectorRegistry.StripeCheckoutV1, Config(),
            ConnectorRegistry.StripeCheckoutV1, Config(origin: "https://new.example"));

        act.Should().NotThrow();
    }

    [Fact]
    public void SelectedSetting_Should_HaveNoDefaultAndRemainPrivate()
    {
        var definition = SettingDefinitions.Get(StripeSettingNames.ConnectorId);
        definition.Should().NotBeNull();
        definition!.DefaultValue.Should().BeNull();
        definition.IsVisibleToClients.Should().BeFalse();
    }

    private static string Config(string origin = "https://shop.example", string account = "acct_merchant", string environment = "sandbox")
        => JsonSerializer.Serialize(new { environment, returnOrigin = origin, accountId = account });

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public DateTime Now { get; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        public FinanceDbContext Db { get; }
        public Mock<IClock> Clock { get; } = new();
        public Mock<ITenantSettingStore> Settings { get; } = new();
        public CredentialBundleService Bundles { get; }
        public StripeConnectorResolver Resolver { get; }

        public Harness()
        {
            Clock.SetupGet(c => c.UtcNow).Returns(Now);
            var tenant = new FixedTenantProvider(TenantId);
            Db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"StripeBindings_{Guid.NewGuid()}").Options, tenant, null, Clock.Object);
            Bundles = new CredentialBundleService(Db, new ConnectorCredentialProtector(new EphemeralDataProtectionProvider()),
                tenant, Mock.Of<IAuditLogWriter>(), Clock.Object);
            Resolver = new StripeConnectorResolver(Db, tenant, Settings.Object, Bundles, Clock.Object);
        }

        public async Task<Connector> SeedAsync()
        {
            await Bundles.UpsertAsync(new CredentialBundleWriteRequest("stripe", "Merchant", ConnectorRegistry.StripeCheckoutV1,
                new Dictionary<string, string> { ["secretKey"] = "sk_test_fixture", ["signingSecret"] = "whsec_old" }));
            var connector = new Connector
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PartnerId = Guid.NewGuid(), Status = "Active",
                ConnectorType = ConnectorRegistry.StripeCheckoutV1, CredentialsRef = "stripe", ConfigJson = Config(),
            };
            Db.Connectors.Add(connector);
            await Db.SaveChangesAsync();
            return connector;
        }

        public void Dispose() => Db.Dispose();
    }

    private sealed class FixedTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid value)
        {
            value = tenantId;
            return true;
        }
    }
}
