using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Endpoints.Admin.Manifest;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 097 §8 / §17: the manifest is authenticated and tenant-scoped — it carries the canonical
/// backend module ids the tenant resolved enabled, and the catalogue with state.
/// </summary>
public class AdminManifestEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminManifestEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminManifest_Should_Return401_When_Unauthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminManifest_Should_ReturnEveryModuleEnabled_When_TenantHasNoRows()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreateTenantAdminClientAsync(tenantId);

        var response = await client.GetAsync("/admin/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminManifestResponse>();
        payload.Should().NotBeNull();

        var allIds = ModuleCatalog.All.Select(descriptor => descriptor.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        payload!.EnabledModules.Should().Equal(allIds, "an absent row means the catalogue default, and the list is sorted");
        payload.EnabledModules.Should().Contain([ModuleIds.Finance, ModuleIds.Commerce, ModuleIds.Agents]);

        payload.Modules.Should().HaveCount(ModuleCatalog.All.Count);
        payload.Modules.Where(module => module.IsCore).Select(module => module.Id)
            .Should().BeEquivalentTo([ModuleIds.Platform, ModuleIds.Ordering, ModuleIds.Ai, ModuleIds.Agents]);
        payload.Modules.Should().OnlyContain(module => module.IsEnabled);
        payload.Modules.Single(module => module.Id == ModuleIds.Commerce).DependsOn
            .Should().BeEquivalentTo([ModuleIds.Ordering, ModuleIds.Finance]);

        payload.FeatureFlags.Should().ContainKey("finance:billing");
        payload.DisabledRoutes.Should().BeEmpty();
        payload.DisabledNavItems.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminManifest_Should_DifferBetweenTenants_When_TheirRowsDiffer()
    {
        var commerceOff = Guid.NewGuid();
        var untouched = Guid.NewGuid();
        var commerceOffClient = await CreateTenantAdminClientAsync(commerceOff);
        var untouchedClient = await CreateTenantAdminClientAsync(untouched);
        await SeedRowAsync(commerceOff, ModuleIds.Commerce, isEnabled: false);

        var commerceOffManifest = await commerceOffClient.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");
        var untouchedManifest = await untouchedClient.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");

        commerceOffManifest!.EnabledModules.Should().NotContain(ModuleIds.Commerce);
        commerceOffManifest.Modules.Single(module => module.Id == ModuleIds.Commerce).IsEnabled.Should().BeFalse();
        commerceOffManifest.EnabledModules.Should().Contain(ModuleIds.Finance);

        untouchedManifest!.EnabledModules.Should().Contain(ModuleIds.Commerce, "the other tenant's row is not this tenant's");
    }

    [Fact]
    public async Task AdminManifest_Should_CloseOverDependencies_When_FinanceIsOff()
    {
        var tenantId = Guid.NewGuid();
        var client = await CreateTenantAdminClientAsync(tenantId);
        await SeedRowAsync(tenantId, ModuleIds.Finance, isEnabled: false);

        var manifest = await client.GetFromJsonAsync<AdminManifestResponse>("/admin/manifest");

        manifest!.EnabledModules.Should().NotContain([ModuleIds.Finance, ModuleIds.Commerce, ModuleIds.Subscriptions, ModuleIds.Workspaces]);
        manifest.EnabledModules.Should().Contain(ModuleCatalog.CoreIds);
        manifest.EnabledModules.Should().Contain(ModuleIds.PersonalFinance, "only a soft dependency on finance");
    }

    [Fact]
    public async Task AdminManifest_Should_BeReadableByAPersonalUser()
    {
        var tenantId = Guid.NewGuid();
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser"));

        var response = await client.GetAsync("/admin/manifest");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "AdminUserPolicy admits every signed-in role");
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
