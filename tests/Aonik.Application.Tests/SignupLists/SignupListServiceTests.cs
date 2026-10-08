using System.Text.Json;

using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.SignupLists;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.SignupLists;

public class SignupListServiceTests
{
    [Theory]
    [InlineData(SignupListTypes.Newsletter)]
    [InlineData(SignupListTypes.DeliveryAvailability)]
    [InlineData(SignupListTypes.PrivateTable)]
    public async Task CaptureAsync_Should_StoreOnlyTheSelectedListsConsent_WithoutCreatingAnAccount(string listType)
    {
        using var harness = new Harness();
        var request = Request(listType) with { Email = "  Cook+Interest@Example.TEST  " };

        await harness.Service.CaptureAsync(listType, request);

        var stored = await harness.Context.SignupSubscriptions.SingleAsync();
        stored.TenantId.Should().Be(harness.TenantId);
        stored.ListType.Should().Be(listType);
        stored.Email.Should().Be("Cook+Interest@Example.TEST");
        stored.NormalizedEmail.Should().Be("cook+interest@example.test");
        stored.ConsentVersion.Should().Be("v1");
        stored.ConsentTextSnapshot.Should().Be(ConsentText(listType));
        stored.ConsentSource.Should().Be(listType + "-form");
        stored.ConsentedAtUtc.Should().Be(harness.Clock.UtcNow);
        stored.UnsubscribedAtUtc.Should().BeNull();
        (await harness.Context.Parties.CountAsync()).Should().Be(0);
        (await harness.Context.Users.CountAsync()).Should().Be(0);
        (await harness.Context.PartyConsents.CountAsync()).Should().Be(0);
        (await harness.Context.ConsentGrants.CountAsync()).Should().Be(0);
        (await harness.Context.MarketingPreferences.CountAsync()).Should().Be(0);
        (await harness.Context.NotificationPreferences.CountAsync()).Should().Be(0);

        if (listType == SignupListTypes.DeliveryAvailability)
        {
            stored.Postcode.Should().Be("SW1A 1AA");
            stored.PostcodeOutwardCode.Should().Be("SW1A");
            stored.Name.Should().BeNull();
            stored.CountryCode.Should().BeNull();
        }
        else if (listType == SignupListTypes.PrivateTable)
        {
            stored.Name.Should().Be("A Cook");
            stored.Phone.Should().Be("+44 (0)20 1234 5678");
            stored.CountryCode.Should().Be("GB");
            stored.Service.Should().Be("recipe-development");
            stored.Postcode.Should().BeNull();
        }
        else
        {
            stored.Postcode.Should().BeNull();
            stored.Name.Should().BeNull();
            stored.Phone.Should().BeNull();
            stored.CountryCode.Should().BeNull();
            stored.Service.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(" sw1a1aa ", "SW1A 1AA", "SW1A")]
    [InlineData("m11ae", "M1 1AE", "M1")]
    [InlineData("eh12ng", "EH1 2NG", "EH1")]
    public async Task CaptureAsync_Should_NormalizeTheFullPostcode_AndRetainTheOutwardCode(
        string postcode, string normalized, string outward)
    {
        using var harness = new Harness();

        await harness.Service.CaptureAsync(SignupListTypes.DeliveryAvailability,
            Request(SignupListTypes.DeliveryAvailability) with { Postcode = postcode });

        var stored = await harness.Context.SignupSubscriptions.SingleAsync();
        stored.Postcode.Should().Be(normalized);
        stored.PostcodeOutwardCode.Should().Be(outward);
    }

    [Fact]
    public async Task CaptureAsync_Should_AllowPrivateTableWithoutPhone_AndPreserveTheConfiguredServiceId()
    {
        using var harness = new Harness();

        await harness.Service.CaptureAsync(SignupListTypes.PrivateTable,
            Request(SignupListTypes.PrivateTable) with { Phone = null, Service = "not-sure" });

        var stored = await harness.Context.SignupSubscriptions.SingleAsync();
        stored.Phone.Should().BeNull();
        stored.Service.Should().Be("not-sure");
    }

    [Fact]
    public async Task CaptureAsync_Should_PreserveTheFirstConsentAndDetails_WhenAnUnverifiedDuplicateArrives()
    {
        using var harness = new Harness();
        await harness.Service.CaptureAsync(SignupListTypes.PrivateTable, Request(SignupListTypes.PrivateTable));
        var original = await harness.Context.SignupSubscriptions.AsNoTracking().SingleAsync();
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddDays(2);
        harness.Configure(Configuration("v2"));

        await harness.Service.CaptureAsync(SignupListTypes.PrivateTable,
            Request(SignupListTypes.PrivateTable) with
            {
                Email = "  COOK@EXAMPLE.TEST ", ConsentVersion = "v2", Name = "Someone else",
                Phone = "999999", Country = "US", Service = "not-sure"
            });

        var stored = await harness.Context.SignupSubscriptions.AsNoTracking().SingleAsync();
        stored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task CaptureAsync_Should_KeepDeliveryInterestIndependentOfNewsletterAndPrivateTable()
    {
        using var harness = new Harness();
        foreach (var listType in ListTypes)
            await harness.Service.CaptureAsync(listType, Request(listType));
        var newsletter = (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Single();

        await harness.Service.UnsubscribeAsync(SignupListTypes.Newsletter, newsletter.Id, newsletter.UnsubscribeToken);

        (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Should().BeEmpty();
        (await harness.Service.ListAsync(SignupListTypes.DeliveryAvailability)).Items.Should().ContainSingle();
        (await harness.Service.ListAsync(SignupListTypes.PrivateTable)).Items.Should().ContainSingle();
        (await harness.Context.SignupSubscriptions.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task CaptureAsync_Should_NotReverseWithdrawal_OrReplaceEvidenceOnARepeatedSignup()
    {
        using var harness = new Harness();
        await harness.Service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));
        var subscription = (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Single();
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddHours(1);
        await harness.Service.UnsubscribeAsync(SignupListTypes.Newsletter, subscription.Id, subscription.UnsubscribeToken);
        var withdrawn = await harness.Context.SignupSubscriptions.AsNoTracking().SingleAsync();
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddDays(1);
        harness.Configure(Configuration("v2"));

        await harness.Service.CaptureAsync(SignupListTypes.Newsletter,
            Request(SignupListTypes.Newsletter) with { ConsentVersion = "v2" });
        var replay = await harness.Service.UnsubscribeAsync(
            SignupListTypes.Newsletter, subscription.Id, subscription.UnsubscribeToken);

        replay.Should().BeTrue();
        (await harness.Context.SignupSubscriptions.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(withdrawn);
        (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Should().BeEmpty();
        var export = (await harness.Service.ListAsync(SignupListTypes.Newsletter, includeUnsubscribed: true)).Items.Single();
        export.UnsubscribedAtUtc.Should().Be(withdrawn.UnsubscribedAtUtc);
        export.UnsubscribeToken.Should().BeNull();
    }

    [Fact]
    public async Task UnsubscribeAsync_Should_ContinueWorking_WhenTheListConfigurationIsRemoved()
    {
        using var harness = new Harness();
        await harness.Service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));
        var row = (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Single();
        harness.Settings.Remove(harness.TenantId);
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddYears(5);

        (await harness.Service.UnsubscribeAsync(SignupListTypes.Newsletter, row.Id, row.UnsubscribeToken))
            .Should().BeTrue();

        (await harness.Context.SignupSubscriptions.SingleAsync()).UnsubscribedAtUtc.Should().Be(harness.Clock.UtcNow);
    }

    [Fact]
    public async Task UnsubscribeAsync_Should_RejectTokensForAnotherTenantListOrSubscription_AndOtherPurposes()
    {
        using var harness = new Harness();
        foreach (var listType in ListTypes)
            await harness.Service.CaptureAsync(listType, Request(listType));
        var rows = await harness.Context.SignupSubscriptions.ToListAsync();
        var newsletter = (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Single();
        var otherTenant = Guid.NewGuid();
        using var otherContext = harness.CreateContext(otherTenant);
        var otherService = harness.CreateService(otherContext, otherTenant);
        var wrongPurpose = harness.Protection.CreateProtector("different-purpose").Protect("unsubscribe");
        var wrongPayload = harness.Protection.CreateProtector("Aonik.Platform.SignupListUnsubscribe.v1",
            harness.TenantId.ToString("N"), SignupListTypes.Newsletter, newsletter.Id.ToString("N")).Protect("subscribe");

        foreach (var token in new[] { null, "", "random-token", newsletter.UnsubscribeToken + "x", wrongPurpose, wrongPayload })
            (await harness.Service.UnsubscribeAsync(SignupListTypes.Newsletter, newsletter.Id, token)).Should().BeFalse();
        (await harness.Service.UnsubscribeAsync(SignupListTypes.Newsletter, Guid.NewGuid(), newsletter.UnsubscribeToken))
            .Should().BeFalse();
        (await harness.Service.UnsubscribeAsync(SignupListTypes.PrivateTable,
            rows.Single(x => x.ListType == SignupListTypes.PrivateTable).Id, newsletter.UnsubscribeToken)).Should().BeFalse();
        (await otherService.UnsubscribeAsync(SignupListTypes.Newsletter, newsletter.Id, newsletter.UnsubscribeToken))
            .Should().BeFalse();

        (await harness.Context.SignupSubscriptions.ToListAsync()).Should().OnlyContain(x => x.UnsubscribedAtUtc == null);
    }

    [Fact]
    public async Task CaptureAndReports_Should_IsolateTenants_WhileAllowingTheSameEmail()
    {
        using var harness = new Harness();
        var otherTenant = Guid.NewGuid();
        harness.Configure(Configuration("other-v1"), otherTenant);
        using var otherContext = harness.CreateContext(otherTenant);
        var otherService = harness.CreateService(otherContext, otherTenant);

        await harness.Service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));
        await otherService.CaptureAsync(SignupListTypes.Newsletter,
            Request(SignupListTypes.Newsletter) with { ConsentVersion = "other-v1" });

        var own = await harness.Service.ListAsync(SignupListTypes.Newsletter);
        var other = await otherService.ListAsync(SignupListTypes.Newsletter);
        own.Items.Should().ContainSingle().Which.ConsentVersion.Should().Be("v1");
        other.Items.Should().ContainSingle().Which.ConsentVersion.Should().Be("other-v1");
        own.Items.Single().Id.Should().NotBe(other.Items.Single().Id);
        (await harness.Context.SignupSubscriptions.AcrossTenants().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CaptureAsync_Should_NotUseAnotherTenantsConfiguration()
    {
        using var harness = new Harness();
        var otherTenant = Guid.NewGuid();
        using var otherContext = harness.CreateContext(otherTenant);
        var service = harness.CreateService(otherContext, otherTenant);

        var act = () => service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));

        await act.Should().ThrowAsync<InvalidStateException>();
        (await harness.Context.SignupSubscriptions.AcrossTenants().CountAsync()).Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CaptureAsync_Should_RejectInvalidOrIrrelevantFields_WithoutSaving(
        string listType, SignupCaptureRequest request)
    {
        using var harness = new Harness();

        var act = () => harness.Service.CaptureAsync(listType, request);

        await act.Should().ThrowAsync<InvalidStateException>();
        (await harness.Context.SignupSubscriptions.CountAsync()).Should().Be(0);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        var newsletter = Request(SignupListTypes.Newsletter);
        yield return ["unknown", newsletter];
        yield return ["Newsletter", newsletter];
        foreach (var email in new[] { "", "not-an-email", "two@example.test,other@example.test" })
            yield return [SignupListTypes.Newsletter, newsletter with { Email = email }];
        yield return [SignupListTypes.Newsletter, newsletter with { ConsentVersion = "" }];
        yield return [SignupListTypes.Newsletter, newsletter with { ConsentVersion = "old-version" }];
        yield return [SignupListTypes.Newsletter, newsletter with { Name = "Unrelated personal data" }];
        yield return [SignupListTypes.Newsletter, newsletter with { Postcode = "SW1A 1AA" }];
        var delivery = Request(SignupListTypes.DeliveryAvailability);
        foreach (var postcode in new string?[] { null, "", "12345", "SW1A", "SW1A 1AA additional text" })
            yield return [SignupListTypes.DeliveryAvailability, delivery with { Postcode = postcode }];
        yield return [SignupListTypes.DeliveryAvailability, delivery with { Service = "not-sure" }];
        var privateTable = Request(SignupListTypes.PrivateTable);
        yield return [SignupListTypes.PrivateTable, privateTable with { Name = null }];
        yield return [SignupListTypes.PrivateTable, privateTable with { Country = null }];
        yield return [SignupListTypes.PrivateTable, privateTable with { Country = "United Kingdom" }];
        foreach (var phone in new[] { "call me maybe", "+12345", "+123456789012345678", "+44\n2012345678" })
            yield return [SignupListTypes.PrivateTable, privateTable with { Phone = phone }];
        yield return [SignupListTypes.PrivateTable, privateTable with { Service = null }];
        yield return [SignupListTypes.PrivateTable, privateTable with { Service = "not-configured" }];
        yield return [SignupListTypes.PrivateTable, privateTable with { Postcode = "SW1A 1AA" }];
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"lists\":null}")]
    [InlineData("{\"lists\":[]}")]
    [InlineData("{\"lists\":[null]}")]
    [InlineData("{\"lists\":[{\"listType\":\"newsletter\",\"consentVersion\":\"v1\",\"consentText\":\"\"}]}")]
    [InlineData("{\"lists\":[{\"listType\":\"newsletter\",\"consentVersion\":\"v1\",\"consentText\":\"Join\"},{\"listType\":\"unknown\",\"consentVersion\":\"v1\",\"consentText\":\"Join\"}]}")]
    [InlineData("{\"lists\":[{\"listType\":\"newsletter\",\"consentVersion\":\"v1\",\"consentText\":\"Join\"},{\"listType\":\"newsletter\",\"consentVersion\":\"v2\",\"consentText\":\"Join\"}]}")]
    [InlineData("{\"lists\":[{\"listType\":\"newsletter\",\"consentVersion\":\"v1\",\"consentText\":\"Join\"},{\"listType\":\"private-table\",\"consentVersion\":\"v1\",\"consentText\":\"Join\",\"services\":[]}]}")]
    public async Task CaptureAsync_Should_RejectDisabledOrInvalidConfiguration(string? json)
    {
        using var harness = new Harness();
        harness.Settings[harness.TenantId] = json;

        var act = () => harness.Service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));

        await act.Should().ThrowAsync<InvalidStateException>();
        (await harness.Context.SignupSubscriptions.CountAsync()).Should().Be(0);
        (await harness.Service.GetConfigurationAsync()).Lists.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfigurationAsync_Should_ReturnOnlyTheTypedPublicContract()
    {
        using var harness = new Harness();
        harness.Settings[harness.TenantId] = """
            {"lists":[{"listType":"newsletter","consentVersion":"v1","consentText":"Kitchen notes.",
              "subscribers":["private@example.test"],"providerSecret":"secret-value"}],"internalNotes":"private-note"}
            """;

        var result = await harness.Service.GetConfigurationAsync();

        result.Lists.Should().ContainSingle().Which.ConsentText.Should().Be("Kitchen notes.");
        var json = JsonSerializer.Serialize(result);
        json.Should().NotContain("private@example.test").And.NotContain("secret-value").And.NotContain("private-note");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Reports_Should_RequireAnAuthenticatedUserWithCustomersRead(bool authenticated, bool permitted)
    {
        using var harness = new Harness();
        var user = authenticated ? new TestCurrentUserProvider() : Mock.Of<ICurrentUserProvider>();
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "Customers.Read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(permitted);
        var service = harness.CreateService(harness.Context, harness.TenantId, user, permissions.Object);

        var list = () => service.ListAsync(SignupListTypes.Newsletter);
        var demand = () => service.GetAreaDemandAsync();

        await list.Should().ThrowAsync<PermissionDeniedException>();
        await demand.Should().ThrowAsync<PermissionDeniedException>();
    }

    [Fact]
    public async Task AnonymousCaptureAndWithdrawal_Should_NotRequireAnAdminPermission()
    {
        using var harness = new Harness();
        var permissions = new Mock<IPermissionService>(MockBehavior.Strict);
        var service = harness.CreateService(harness.Context, harness.TenantId,
            Mock.Of<ICurrentUserProvider>(), permissions.Object);
        await service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));
        var row = (await harness.Service.ListAsync(SignupListTypes.Newsletter)).Items.Single();

        var result = await service.UnsubscribeAsync(SignupListTypes.Newsletter, row.Id, row.UnsubscribeToken);

        result.Should().BeTrue();
        permissions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task ListAsync_Should_RejectUnboundedOrOverflowingPagination(int pageNumber, int pageSize)
    {
        using var harness = new Harness();

        var act = () => harness.Service.ListAsync(SignupListTypes.Newsletter,
            pageNumber: pageNumber, pageSize: pageSize);

        await act.Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task Reports_Should_PageActiveSubscribers_AndCountOnlyCurrentDeliveryInterest()
    {
        using var harness = new Harness();
        foreach (var (email, postcode) in new[]
        {
            ("first@example.test", "SW1A 1AA"), ("second@example.test", "SW1A 2AA"),
            ("third@example.test", "M1 1AE"), ("withdrawn@example.test", "M1 2AB")
        })
            await harness.Service.CaptureAsync(SignupListTypes.DeliveryAvailability,
                Request(SignupListTypes.DeliveryAvailability) with { Email = email, Postcode = postcode });
        await harness.Service.CaptureAsync(SignupListTypes.DeliveryAvailability,
            Request(SignupListTypes.DeliveryAvailability) with { Email = "FIRST@example.test" });
        await harness.Service.CaptureAsync(SignupListTypes.Newsletter, Request(SignupListTypes.Newsletter));
        var withdrawn = (await harness.Service.ListAsync(SignupListTypes.DeliveryAvailability)).Items
            .Single(x => x.Email == "withdrawn@example.test");
        await harness.Service.UnsubscribeAsync(SignupListTypes.DeliveryAvailability, withdrawn.Id, withdrawn.UnsubscribeToken);
        var otherTenant = Guid.NewGuid();
        harness.Configure(Configuration(), otherTenant);
        using var otherContext = harness.CreateContext(otherTenant);
        await harness.CreateService(otherContext, otherTenant).CaptureAsync(SignupListTypes.DeliveryAvailability,
            Request(SignupListTypes.DeliveryAvailability));

        var firstPage = await harness.Service.ListAsync(SignupListTypes.DeliveryAvailability, pageNumber: 1, pageSize: 2);
        var secondPage = await harness.Service.ListAsync(SignupListTypes.DeliveryAvailability, pageNumber: 2, pageSize: 2);
        var demand = await harness.Service.GetAreaDemandAsync();

        firstPage.TotalCount.Should().Be(3);
        firstPage.Items.Should().HaveCount(2).And.OnlyContain(x => x.UnsubscribeToken != null);
        secondPage.Items.Should().ContainSingle();
        firstPage.Items.Select(x => x.Id).Intersect(secondPage.Items.Select(x => x.Id)).Should().BeEmpty();
        demand.Should().BeEquivalentTo(new[] { new SignupAreaDemandDto("SW1A", 2), new SignupAreaDemandDto("M1", 1) });
    }

    private static readonly string[] ListTypes =
        [SignupListTypes.Newsletter, SignupListTypes.DeliveryAvailability, SignupListTypes.PrivateTable];

    private static string ConsentText(string listType) => $"Exact consent for {listType}.";

    private static SignupListsConfigurationDto Configuration(string version = "v1") => new(
        ListTypes.Select(listType => new SignupListDefinitionDto(listType, version, ConsentText(listType),
            listType == SignupListTypes.PrivateTable
                ? [new("recipe-development", "Recipe development"), new("not-sure", "Not sure yet")]
                : null)).ToList());

    private static SignupCaptureRequest Request(string listType) => listType switch
    {
        SignupListTypes.DeliveryAvailability => new("cook@example.test", "v1", Postcode: " sw1a1aa "),
        SignupListTypes.PrivateTable => new("cook@example.test", "v1", Name: " A Cook ",
            Phone: " +44 (0)20 1234 5678 ", Country: "gb", Service: "recipe-development"),
        _ => new("cook@example.test", "v1")
    };

    private sealed class Harness : IDisposable
    {
        private readonly DbContextOptions<PlatformDbContext> _options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"SignupLists_{Guid.NewGuid()}").Options;
        private readonly Mock<ITenantSettingStore> _settings = new();
        public Guid TenantId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
        public IDataProtectionProvider Protection { get; } = new EphemeralDataProtectionProvider();
        public Dictionary<Guid, string?> Settings { get; } = [];
        public PlatformDbContext Context { get; }
        public SignupListService Service { get; }

        public Harness()
        {
            _settings.Setup(x => x.GetTenantValueAsync(SignupListSettingNames.Configuration,
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((string _, Guid tenantId, CancellationToken _) =>
                    Task.FromResult(Settings.GetValueOrDefault(tenantId)));
            Configure(Configuration());
            Context = CreateContext(TenantId);
            Service = CreateService(Context, TenantId);
        }

        public void Configure(SignupListsConfigurationDto configuration, Guid? tenantId = null) =>
            Settings[tenantId ?? TenantId] = JsonSerializer.Serialize(configuration, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        public PlatformDbContext CreateContext(Guid tenantId) =>
            new(_options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);

        public SignupListService CreateService(PlatformDbContext context, Guid tenantId,
            ICurrentUserProvider? currentUser = null, IPermissionService? permissionService = null)
        {
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "Customers.Read", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            return new SignupListService(context, new TestTenantProvider(tenantId), Clock, _settings.Object, Protection,
                currentUser ?? new TestCurrentUserProvider(), permissionService ?? permissions.Object);
        }

        public void Dispose() => Context.Dispose();
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    }
}
