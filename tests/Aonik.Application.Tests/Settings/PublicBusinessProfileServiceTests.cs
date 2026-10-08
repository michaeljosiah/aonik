using System.Text.Json;

using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Settings;

public class PublicBusinessProfileServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetCurrentAsync_Should_ReturnTheExplicitPublicFacts_IncludingClosureOverrides()
    {
        var profile = new PublicBusinessProfileDto("Neighbourhood Kitchen",
            LogoUrl: "https://example.test/logo.png", Website: "https://example.test",
            Contact: new("orders@example.test", "+442012345678", "+442087654321"),
            Legal: new("Kitchen Ltd", "12345678", "1 Public Street, London", "ZA123456", true, "GB123456789"),
            OpeningHours: new("Europe/London",
                [new(1, new(9, 0), new(17, 0))],
                [new(2026, 12, 28)], [new(2026, 12, 21)]));
        var service = CreateService(Serialize(profile));

        var result = await service.GetCurrentAsync();

        result.Should().BeEquivalentTo(profile);
    }

    [Fact]
    public async Task GetCurrentAsync_Should_DistinguishUnconfiguredHours_FromExplicitlyClosedAllWeek()
    {
        var unconfigured = await CreateService(Serialize(new("Kitchen"))).GetCurrentAsync();
        var closed = await CreateService(Serialize(new("Kitchen", OpeningHours: new("Europe/London", [], [], []))))
            .GetCurrentAsync();

        unconfigured!.OpeningHours.Should().BeNull();
        unconfigured.Contact.Should().BeNull();
        unconfigured.Legal.Should().BeNull();
        closed!.OpeningHours.Should().NotBeNull();
        closed.OpeningHours!.WeeklyHours.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"isPublished\":false,\"profile\":{\"displayName\":\"Draft\"}}")]
    [InlineData("{\"isPublished\":true,\"profile\":null}")]
    [InlineData("{\"isPublished\":true,\"profile\":{\"displayName\":\"  \"}}")]
    public async Task GetCurrentAsync_Should_WithholdMissingUnpublishedOrMalformedDocuments(string? json)
    {
        var result = await CreateService(json).GetCurrentAsync();

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 9, 17)]
    [InlineData(8, 9, 17)]
    [InlineData(1, 9, 9)]
    [InlineData(1, 17, 9)]
    public async Task GetCurrentAsync_Should_WithholdInvalidDaysAndNonIncreasingPeriods(int day, int opens, int closes)
    {
        var profile = new PublicBusinessProfileDto("Kitchen", OpeningHours: new("Europe/London",
            [new(day, new(opens, 0), new(closes, 0))], [], []));

        (await CreateService(Serialize(profile)).GetCurrentAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData(12, false)]
    [InlineData(13, true)]
    public async Task GetCurrentAsync_Should_RejectOverlap_ButAllowTouchingSplitOpeningPeriods(int secondOpeningHour, bool isValid)
    {
        var profile = new PublicBusinessProfileDto("Kitchen", OpeningHours: new("Europe/London",
            [new(1, new(secondOpeningHour, 0), new(17, 0)), new(1, new(9, 0), new(13, 0))], [], []));

        var result = await CreateService(Serialize(profile)).GetCurrentAsync();

        if (isValid) result.Should().BeEquivalentTo(profile);
        else result.Should().BeNull();
    }

    [Theory]
    [InlineData("{\"timezone\":\"Missing/Zone\",\"weeklyHours\":[],\"bankHolidays\":[],\"exceptionalClosures\":[]}")]
    [InlineData("{\"timezone\":\"GMT Standard Time\",\"weeklyHours\":[],\"bankHolidays\":[],\"exceptionalClosures\":[]}")]
    [InlineData("{\"timezone\":\"Europe/London\",\"weeklyHours\":null,\"bankHolidays\":[],\"exceptionalClosures\":[]}")]
    [InlineData("{\"timezone\":\"Europe/London\",\"weeklyHours\":[],\"exceptionalClosures\":[]}")]
    [InlineData("{\"timezone\":\"Europe/London\",\"weeklyHours\":[],\"bankHolidays\":[]}")]
    [InlineData("{\"timezone\":\"Europe/London\",\"weeklyHours\":[null],\"bankHolidays\":[],\"exceptionalClosures\":[]}")]
    [InlineData("{\"timezone\":\"Europe/London\",\"weeklyHours\":[{\"dayOfWeek\":1,\"closesAt\":\"17:00:00\"}],\"bankHolidays\":[],\"exceptionalClosures\":[]}")]
    [InlineData("{\"timezone\":\"Europe/London\",\"weeklyHours\":[],\"bankHolidays\":[\"not-a-date\"],\"exceptionalClosures\":[]}")]
    public async Task GetCurrentAsync_Should_WithholdMalformedHours_InsteadOfInventingFacts(string hoursJson)
    {
        var json = "{\"isPublished\":true,\"profile\":{\"displayName\":\"Kitchen\",\"openingHours\":" + hoursJson + "}}";

        (await CreateService(json).GetCurrentAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData(null, "data:image/svg+xml,test")]
    [InlineData("/relative-site", null)]
    [InlineData(null, "file:///private-logo.png")]
    public async Task GetCurrentAsync_Should_WithholdUnsafeWebsiteOrLogoUrls(string? website, string? logo)
    {
        var profile = new PublicBusinessProfileDto("Kitchen", Website: website, LogoUrl: logo);

        (await CreateService(Serialize(profile)).GetCurrentAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentAsync_Should_AllowOnlyPublicContractFields_FromStoredJson()
    {
        var service = CreateService("""
            {"isPublished":true,"internalApiKey":"wrapper-secret","profile":{
              "displayName":"Kitchen","tenantId":"private-tenant","contactEmail":"private@example.test",
              "contact":{"phone":"123","password":"contact-secret"},
              "legal":{"companyName":"Public Ltd","bankAccount":"private-account"}}}
            """);

        var result = await service.GetCurrentAsync();
        var publicJson = JsonSerializer.Serialize(result, JsonOptions);

        result!.Contact!.Email.Should().BeNull();
        result.Legal!.CompanyName.Should().Be("Public Ltd");
        publicJson.Should().NotContain("private").And.NotContain("secret").And.NotContain("isPublished");
    }

    [Fact]
    public async Task GetCurrentAsync_Should_ReadOnlyTheCurrentTenantsOwnSetting()
    {
        var currentTenant = Guid.NewGuid();
        var anotherTenant = Guid.NewGuid();
        var store = new TestStore();
        store.Values[(anotherTenant, BusinessProfileSettingNames.Profile)] = Serialize(new("Other kitchen"));
        var service = new PublicBusinessProfileService(store, new TestTenantProvider(currentTenant));

        (await service.GetCurrentAsync()).Should().BeNull();
        store.Values[(currentTenant, BusinessProfileSettingNames.Profile)] = Serialize(new("This kitchen"));
        (await service.GetCurrentAsync())!.DisplayName.Should().Be("This kitchen");
    }

    private static string Serialize(PublicBusinessProfileDto profile)
        => JsonSerializer.Serialize(new PublicBusinessProfileDocument(true, profile), JsonOptions);

    private static PublicBusinessProfileService CreateService(string? json)
    {
        var tenantId = Guid.NewGuid();
        var store = new TestStore();
        store.Values[(tenantId, BusinessProfileSettingNames.Profile)] = json;
        return new PublicBusinessProfileService(store, new TestTenantProvider(tenantId));
    }

    private sealed class TestStore : ITenantSettingStore
    {
        public Dictionary<(Guid TenantId, string Key), string?> Values { get; } = [];

        public Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Values.GetValueOrDefault((tenantId, key)));

        public Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken cancellationToken = default)
        {
            Values[(tenantId, key)] = value;
            return Task.CompletedTask;
        }
    }
}
