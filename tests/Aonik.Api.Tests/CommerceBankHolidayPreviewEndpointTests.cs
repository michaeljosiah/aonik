using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

namespace Aonik.Api.Tests;

public sealed class CommerceBankHolidayPreviewEndpointTests
    : IClassFixture<CommerceBankHolidayPreviewEndpointTests.HolidayFactory>
{
    private const string Path = "/commerce/admin/fulfilment-calendar/bank-holidays/";
    private const string SavedClosures = "[\"2027-01-04\",\"2027-12-24\"]";
    private readonly HolidayFactory _factory;

    public CommerceBankHolidayPreviewEndpointTests(HolidayFactory factory) => _factory = factory;

    [Theory]
    [InlineData("england-and-wales", HttpStatusCode.OK)]
    [InlineData("northern-ireland", HttpStatusCode.ServiceUnavailable)]
    public async Task Preview_Should_PreserveAllSavedClosures_OnSuccessAndSourceFailure(string region, HttpStatusCode expected)
    {
        var tenantId = await SeedTenantAsync();
        using var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("ReadOnly").WithTenant(tenantId));

        using var response = await client.GetAsync(Path + region);

        response.StatusCode.Should().Be(expected);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        if (expected == HttpStatusCode.OK)
        {
            var preview = await response.Content.ReadFromJsonAsync<BankHolidayPreviewDto>();
            preview!.Region.Should().Be(region);
            preview.Source.Should().Be("https://www.gov.uk/bank-holidays.json");
            preview.Holidays.Should().Equal(new BankHolidayDto(new DateOnly(2027, 1, 1), "New Year’s Day"));
        }
        else
        {
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            error.GetProperty("code").GetString().Should().Be("commerce.bank_holidays_unavailable");
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var calendar = await scope.ServiceProvider.GetRequiredService<AonikDbContext>().FulfilmentCalendars
            .AsNoTracking().SingleAsync(row => row.TenantId == tenantId);
        calendar.BlackoutDatesJson.Should().Be(SavedClosures);
    }

    [Theory]
    [InlineData("wales")]
    [InlineData("Scotland")]
    [InlineData("unknown")]
    public async Task Preview_Should_RejectUnsupportedRegionsWithoutCallingTheSource(string region)
    {
        var tenantId = await SeedTenantAsync();
        using var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        var callsBefore = _factory.Calls.Count;

        using var response = await client.GetAsync(Path + region);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        _factory.Calls.Count.Should().Be(callsBefore);
    }

    [Fact]
    public async Task Preview_Should_RequireAnAdminReadRole()
    {
        var tenantId = await SeedTenantAsync();
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        using var customer = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("PersonalUser").WithTenant(tenantId));
        var callsBefore = _factory.Calls.Count;

        using var unsignedResponse = await anonymous.GetAsync(Path + "scotland");
        using var customerResponse = await customer.GetAsync(Path + "scotland");

        unsignedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unsignedResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        customerResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        _factory.Calls.Count.Should().Be(callsBefore);
    }

    [Fact]
    public async Task Preview_Should_HonorTheTenantCommerceModuleGate()
    {
        var tenantId = await SeedTenantAsync(moduleEnabled: false);
        using var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        var callsBefore = _factory.Calls.Count;

        using var response = await client.GetAsync(Path + "scotland");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("code").GetString().Should().Be(ModuleErrorCodes.Disabled);
        _factory.Calls.Count.Should().Be(callsBefore);
    }

    private async Task<Guid> SeedTenantAsync(bool moduleEnabled = true)
    {
        var tenantId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Holiday preview test", Environment = "Testing", Status = TenantStatus.Active,
            DefaultCurrency = "GBP", SupportedCountriesJson = "[]"
        });
        db.FulfilmentCalendars.Add(new FulfilmentCalendar
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Timezone = "Europe/London",
            DeliveryDaysJson = "[\"thursday\"]", BlackoutDatesJson = SavedClosures,
            CutoffLocalTime = new TimeOnly(12, 0), LeadDays = 7, IsActive = true
        });
        if (!moduleEnabled)
        {
            db.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ModuleId = ModuleIds.Commerce,
                IsEnabled = false, Source = TenantModuleSource.Explicit, Reason = "Holiday preview test"
            });
        }
        await db.SaveChangesAsync();
        return tenantId;
    }

    public sealed class HolidayFactory : CustomWebApplicationFactory
    {
        public ConcurrentQueue<string> Calls { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBankHolidaySource>();
                services.AddSingleton<IBankHolidaySource>(new FixtureHolidaySource(Calls));
            });
        }
    }

    private sealed class FixtureHolidaySource(ConcurrentQueue<string> calls) : IBankHolidaySource
    {
        public Task<BankHolidayPreviewDto?> GetAsync(string region, CancellationToken cancellationToken = default)
        {
            calls.Enqueue(region);
            return Task.FromResult(region == "northern-ireland" ? null : new BankHolidayPreviewDto(
                region, "https://www.gov.uk/bank-holidays.json",
                [new BankHolidayDto(new DateOnly(2027, 1, 1), "New Year’s Day")]));
        }
    }
}
