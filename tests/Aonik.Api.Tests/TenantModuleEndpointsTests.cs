using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Api.Modules;
using Aonik.Platform.Endpoints.Admin.Manifest;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 097 §9 / §17: GET and PUT /admin/tenants/{tenantId}/modules — who may read, who may write,
/// the validator's rejection of core ids, the typed 409s, and the manifest reflecting a toggle.
/// </summary>
public class TenantModuleEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TenantModuleEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── GET ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetModules_Should_ReturnEveryCatalogueModule_When_CallerIsTenantAdminOfThatTenant()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreateTenantAdminClientAsync(tenantId);

        var response = await client.GetAsync($"/admin/tenants/{tenantId}/modules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TenantModuleListResponse>();
        payload!.TenantId.Should().Be(tenantId);
        payload.Modules.Select(module => module.ModuleId).Should().Equal(ModuleCatalog.All.Select(descriptor => descriptor.Id));
        payload.Modules.Single(module => module.ModuleId == ModuleIds.Platform).Source.Should().Be("core");
        payload.Modules.Single(module => module.ModuleId == ModuleIds.Commerce).Source.Should().Be("default");
        payload.Modules.Should().OnlyContain(module => module.IsEnabled);
    }

    [Fact]
    public async Task GetModules_Should_Return403_When_TenantAdminReadsAnotherTenant()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();
        var client = await CreateTenantAdminClientAsync(own);
        await CreateTenantAdminClientAsync(other); // ensures the other tenant exists

        var response = await client.GetAsync($"/admin/tenants/{other}/modules");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetModules_Should_Return200_When_PlatformAdminWithTenantsReadReadsAnotherTenant()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();
        await CreateTenantAdminClientAsync(other); // ensures the other tenant exists
        var client = await _factory.CreateAuthenticatedClientAsync(PlatformAdminOptions(own).WithPermissions("Tenants.Read"));

        var response = await client.GetAsync($"/admin/tenants/{other}/modules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModules_Should_Return404_When_TenantDoesNotExist()
    {
        var own = Guid.NewGuid();
        var client = await _factory.CreateAuthenticatedClientAsync(PlatformAdminOptions(own).WithPermissions("Tenants.Read"));

        var response = await client.GetAsync($"/admin/tenants/{Guid.NewGuid()}/modules");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT: authorisation and validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateModules_Should_Return403_When_CallerIsTenantAdmin()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreateTenantAdminClientAsync(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Commerce, false)]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "only host admins change module state");
    }

    [Fact]
    public async Task UpdateModules_Should_Return200_When_CallerIsPlatformAdmin()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Commerce, false, "no shop")]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TenantModuleListResponse>();
        var commerce = payload!.Modules.Single(module => module.ModuleId == ModuleIds.Commerce);
        commerce.IsEnabled.Should().BeFalse();
        commerce.Source.Should().Be("explicit");
        commerce.Reason.Should().Be("no shop");
        commerce.UpdatedAt.Should().NotBeNull();
        payload.Modules.Single(module => module.ModuleId == ModuleIds.Finance).IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(ModuleIds.Platform)]
    [InlineData(ModuleIds.Ordering)]
    [InlineData(ModuleIds.Ai)]
    [InlineData(ModuleIds.Agents)]
    public async Task UpdateModules_Should_Return422_When_RequestNamesACoreModule(string coreModuleId)
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(coreModuleId, false)]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("core");
    }

    [Fact]
    public async Task UpdateModules_Should_Return422_When_RequestNamesAnUnknownModule()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest("not-a-module", false)]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateModules_Should_Return422_When_RequestIsEmptyOrRepeatsAModule()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var empty = await client.PutAsJsonAsync($"/admin/tenants/{tenantId}/modules", new TenantModuleUpdateRequest([]));
        var duplicated = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest(
            [
                new TenantModuleToggleRequest(ModuleIds.Commerce, false),
                new TenantModuleToggleRequest(ModuleIds.Commerce, true),
            ]));

        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        duplicated.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── PUT: dependency conflicts ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateModules_Should_Return409DependentsEnabled_When_DisablingFinanceWhileCommerceIsOn()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Finance, false)]));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be(ModuleErrorCodes.DependentsEnabled);
        body.RootElement.GetProperty("moduleId").GetString().Should().Be(ModuleIds.Finance);
        body.RootElement.GetProperty("relatedModuleIds").EnumerateArray().Select(element => element.GetString())
            .Should().Equal(ModuleIds.Commerce, ModuleIds.Subscriptions, ModuleIds.Workspaces);
        body.RootElement.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateModules_Should_Return409DependencyMissing_When_EnablingCommerceWhileFinanceIsOff()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);
        await SeedRowAsync(tenantId, ModuleIds.Finance, isEnabled: false);
        await SeedRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Commerce, true)]));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be(ModuleErrorCodes.DependencyMissing);
        body.RootElement.GetProperty("moduleId").GetString().Should().Be(ModuleIds.Commerce);
        body.RootElement.GetProperty("relatedModuleIds").EnumerateArray().Select(element => element.GetString())
            .Should().Equal(ModuleIds.Finance);
    }

    [Fact]
    public async Task UpdateModules_Should_Return200_When_TheCascadeIsIncludedInTheRequest()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest(
            [
                new TenantModuleToggleRequest(ModuleIds.Finance, false),
                new TenantModuleToggleRequest(ModuleIds.Commerce, false),
                new TenantModuleToggleRequest(ModuleIds.Subscriptions, false),
                new TenantModuleToggleRequest(ModuleIds.Workspaces, false),
            ]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TenantModuleListResponse>();
        payload!.Modules.Where(module => !module.IsEnabled).Select(module => module.ModuleId)
            .Should().BeEquivalentTo([ModuleIds.Finance, ModuleIds.Commerce, ModuleIds.Subscriptions, ModuleIds.Workspaces]);
    }

    // ── PUT then manifest ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Manifest_Should_ReflectTheToggle_AfterAnUpdate()
    {
        var tenantId = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var adminClient = await CreatePlatformAdminClientAsync(tenantId);
        var otherClient = await CreateTenantAdminClientAsync(otherTenant);

        var before = await adminClient.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");
        before!.EnabledModules.Should().Contain(ModuleIds.Commerce);

        var update = await adminClient.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Commerce, false)]));
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await adminClient.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");
        after!.EnabledModules.Should().NotContain(ModuleIds.Commerce, "the write invalidates the cached set, so the next manifest read sees the row");
        after.Modules.Single(module => module.Id == ModuleIds.Commerce).IsEnabled.Should().BeFalse();

        var other = await otherClient.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");
        other!.EnabledModules.Should().Contain(ModuleIds.Commerce, "no other tenant's manifest changes");

        var restore = await adminClient.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Commerce, true)]));
        restore.StatusCode.Should().Be(HttpStatusCode.OK);

        var restored = await adminClient.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");
        restored!.EnabledModules.Should().Contain(ModuleIds.Commerce, "switching it back on needs no restart");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static TestAuthOptions PlatformAdminOptions(Guid tenantId)
        => TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("PlatformAdmin")
            .WithClaims(new Claim("roles", "Aonik.PlatformAdmin"));

    private async Task<HttpClient> CreatePlatformAdminClientAsync(Guid tenantId)
    {
        var client = await _factory.CreateAuthenticatedClientAsync(PlatformAdminOptions(tenantId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task<HttpClient> CreateTenantAdminClientAsync(Guid tenantId)
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin"));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task SeedRowAsync(Guid tenantId, string moduleId, bool isEnabled)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        dbContext.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            IsEnabled = isEnabled,
            Source = TenantModuleSource.Explicit,
        });
        await dbContext.SaveChangesAsync();
    }
}
