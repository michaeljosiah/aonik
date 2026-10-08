using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public class DeliveryCoverageServiceTests
{
    [Theory]
    [InlineData("sw1a1aa")]
    [InlineData("  Sw1a 1aA  ")]
    public async Task Check_Should_NormalizeBeforeLookup_AndNeverInventAnEarliestDate(string input)
    {
        var h = new Harness();
        h.Configure(new(true, ["SW1A"]));

        var result = await h.Service().CheckAsync(input);

        result.Status.Should().Be("serves");
        result.NormalisedPostcode.Should().Be("SW1A 1AA");
        result.EarliestDate.Should().BeNull();
        h.Lookup.Verify(service => service.LookupAsync("SW1A 1AA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SW1")]
    [InlineData("SW1A 1A")]
    [InlineData("SW1A\n1AA")]
    [InlineData("ＳW1A 1AA")]
    [InlineData("SW1A 1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Check_Should_ReportMalformedInputAsAFieldError_WithoutLookup(string? input)
    {
        var h = new Harness();
        h.Configure(new(true, ["SW1A"]));

        var check = () => h.Service().CheckAsync(input);

        var error = (await check.Should().ThrowAsync<DeliveryCoverageException>()).Which;
        error.Code.Should().Be("commerce.invalid_postcode");
        error.FieldName.Should().NotBeNullOrWhiteSpace();
        h.Lookup.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"isEnabled\":true}")]
    [InlineData("{\"isEnabled\":true,\"allowedOutwardCodes\":null}")]
    [InlineData("{\"allowedOutwardCodes\":[\"SW1A\"]}")]
    [InlineData("{\"isEnabled\":true,\"allowedOutwardCodes\":[\"SW1A\"],\"excludedOutwardCode\":[\"SW1A\"]}")]
    [InlineData("{\"isEnabled\":true,\"allowedOutwardCodes\":[\"SW*\"]}")]
    [InlineData("{\"isEnabled\":true,\"allowedOutwardCodes\":[\"SW1A\"],\"excludedOutwardCodes\":[\"*\"]}")]
    [InlineData("{\"isEnabled\":false,\"allowedOutwardCodes\":[\"SW1A\"],\"source\":\"approved courier file\"}")]
    public async Task Check_Should_ReturnUnavailable_ForAbsentDisabledOrInvalidConfiguration(string? document)
    {
        var h = new Harness();
        h.Values[h.TenantId] = document;

        var result = await h.Service().CheckAsync("SW1A 1AA");

        result.Status.Should().Be("unavailable");
        result.EarliestDate.Should().BeNull();
        h.Lookup.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("SW1", false, "not_served")]
    [InlineData("SW1A", false, "serves")]
    [InlineData("SW1A", true, "not_served")]
    public async Task Check_Should_MatchTheWholeOutwardCode_WithExclusionsWinning(string allowed, bool excluded, string expected)
    {
        var h = new Harness();
        h.Configure(new(true, [allowed], excluded ? ["SW1A"] : []));

        var result = await h.Service().CheckAsync("SW1A 1AA");

        result.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(PostcodeLookupStatus.Found, "not_served")]
    [InlineData(PostcodeLookupStatus.Unavailable, "unavailable")]
    public async Task Check_WithAnEnabledEmptyList_Should_StillRequireRealPostcodeExistence(PostcodeLookupStatus lookupStatus, string expected)
    {
        var h = new Harness();
        h.Configure(new(true, []));
        h.Lookup.Setup(service => service.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostcodeLookupResult(lookupStatus, lookupStatus == PostcodeLookupStatus.Found ? "SW1A 1AA" : null));

        var result = await h.Service().CheckAsync("SW1A 1AA");

        result.Status.Should().Be(expected);
        h.Lookup.Verify(service => service.LookupAsync("SW1A 1AA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Check_Should_TreatUnknownOrTerminatedPostcodeAsInvalid_EvenWithDenyAllCoverage(bool denyAll)
    {
        var h = new Harness();
        h.Configure(new(true, denyAll ? [] : ["SW1A"]));
        h.Lookup.Setup(service => service.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostcodeLookupResult(PostcodeLookupStatus.NotFound));

        var check = () => h.Service().CheckAsync("SW1A 1ZZ");

        (await check.Should().ThrowAsync<DeliveryCoverageException>()).Which.Code.Should().Be("commerce.invalid_postcode");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a postcode")]
    [InlineData("M1 1AA")]
    public async Task Check_Should_NotApproveAnInvalidOrDifferentProviderResult(string? canonical)
    {
        var h = new Harness();
        h.Configure(new(true, ["SW1A", "M1"]));
        h.Lookup.Setup(service => service.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostcodeLookupResult(PostcodeLookupStatus.Found, canonical));

        var result = await h.Service().CheckAsync("SW1A 1AA");

        result.Status.Should().Be("unavailable");
    }

    [Fact]
    public async Task Check_Should_PreserveCallerCancellation()
    {
        var h = new Harness();
        h.Configure(new(true, ["SW1A"]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var expected = new OperationCanceledException(cancellation.Token);
        h.Lookup.Setup(service => service.LookupAsync("SW1A 1AA", cancellation.Token)).ThrowsAsync(expected);

        var check = () => h.Service().CheckAsync("SW1A 1AA", cancellation.Token);

        (await check.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task Configuration_Should_ReadAndWriteOnlyTheResolvedTenant_WithoutCrossTenantFallback()
    {
        var h = new Harness();
        var otherTenant = Guid.NewGuid();
        h.Configure(new(true, ["M1"]), otherTenant);
        var before = h.Values[otherTenant];
        (await h.Service().GetConfigurationAsync()).Should().BeNull();
        (await h.Service().CheckAsync("SW1A 1AA")).Status.Should().Be("unavailable");

        var saved = await h.Service().UpdateConfigurationAsync(new(true, [" sw1a ", "SW1A"], [" m1 "], " courier file "));

        saved.AllowedOutwardCodes.Should().Equal("SW1A");
        saved.ExcludedOutwardCodes.Should().Equal("M1");
        saved.Source.Should().Be("courier file");
        (await h.Service().GetConfigurationAsync()).Should().BeEquivalentTo(saved);
        (await h.Service().CheckAsync("SW1A 1AA")).Status.Should().Be("serves");
        (await h.Service(otherTenant).CheckAsync("SW1A 1AA")).Status.Should().Be("not_served");
        h.Values[otherTenant].Should().Be(before);
        h.Store.Verify(settings => settings.SetTenantValueAsync(Harness.Key, It.IsAny<string?>(), h.TenantId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("missing-list")]
    [InlineData("wildcard-allow")]
    [InlineData("wildcard-exclude")]
    [InlineData("source-too-long")]
    [InlineData("source-control")]
    [InlineData("document-too-long")]
    public async Task Configuration_Should_RejectInvalidReplacement_WithoutChangingTheExistingRules(string invalid)
    {
        var h = new Harness();
        h.Configure(new(true, ["SW1A"]));
        var original = h.Values[h.TenantId];
        DeliveryCoverageConfigDto replacement = invalid switch
        {
            "missing-list" => new DeliveryCoverageConfigDto(true, null!),
            "wildcard-allow" => new(true, ["SW*"]),
            "wildcard-exclude" => new(true, ["SW1A"], ["SW*"]),
            "source-too-long" => new(true, ["SW1A"], Source: new string('a', 257)),
            "source-control" => new(true, ["SW1A"], Source: "courier\nfile"),
            _ => new(true, Enumerable.Range(0, 1000).Select(index => $"{(char)('A' + index / 100)}{index % 100:00}").ToArray()),
        };

        var update = () => h.Service().UpdateConfigurationAsync(replacement);

        await update.Should().ThrowAsync<StorefrontValidationException>();
        h.Values[h.TenantId].Should().Be(original);
        h.Store.Verify(settings => settings.SetTenantValueAsync(It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Configuration_Should_FailClosed_WhenStoredDocumentExceedsTheSettingsBound()
    {
        var h = new Harness();
        h.Configure(new(true, ["SW1A"], Source: new string('x', 4001)));

        (await h.Service().GetConfigurationAsync()).Should().BeNull();
        (await h.Service().CheckAsync("SW1A 1AA")).Status.Should().Be("unavailable");
        h.Lookup.VerifyNoOtherCalls();
    }

    internal sealed class Harness
    {
        public const string Key = CommerceSettingNames.DeliveryCoverage;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public Guid TenantId { get; }
        public Dictionary<Guid, string?> Values { get; } = [];
        public Mock<ITenantSettingStore> Store { get; } = new(MockBehavior.Strict);
        public Mock<IPostcodeLookup> Lookup { get; } = new(MockBehavior.Strict);

        public Harness(Guid? tenantId = null)
        {
            TenantId = tenantId ?? Guid.NewGuid();
            Store.Setup(settings => settings.GetTenantValueAsync(Key, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, Guid tenant, CancellationToken _) => Values.GetValueOrDefault(tenant));
            Store.Setup(settings => settings.SetTenantValueAsync(Key, It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Callback<string, string?, Guid, CancellationToken>((_, value, tenant, _) => Values[tenant] = value)
                .Returns(Task.CompletedTask);
            Lookup.Setup(service => service.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string postcode, CancellationToken _) => new PostcodeLookupResult(PostcodeLookupStatus.Found, postcode));
        }

        public void Configure(DeliveryCoverageConfigDto configuration, Guid? tenantId = null)
            => Values[tenantId ?? TenantId] = JsonSerializer.Serialize(configuration, JsonOptions);

        public DeliveryCoverageService Service(Guid? tenantId = null)
            => new(Store.Object, new TestTenantProvider(tenantId ?? TenantId), Lookup.Object);
    }
}
