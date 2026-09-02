using System.Net;
using System.Text.Json;

using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Services.Packs;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 097 acceptance 13 through the real host: a tenant provisioned from the revised <c>simi</c>
/// pack (personal-finance, groups, documents, ai, agents, voice) is NOT gated on the modules the pack
/// declares, and IS gated on the ones it leaves off (finance). The pack rows are written through the
/// production applier, not seeded by hand.
/// </summary>
public class SimiPackTenantModuleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SimiPackTenantModuleTests(CustomWebApplicationFactory factory) => _factory = factory;

    public static TheoryData<string, string, string> DeclaredModuleEndpoints => new()
    {
        { ModuleIds.Documents, "/documents", "Operations" },
        { ModuleIds.Voice, "/tenant/settings/voice/recipes", "TenantAdmin" },
        { ModuleIds.PersonalFinance, "/personal-finance/categories", "PersonalUser" },
    };

    [Theory]
    [MemberData(nameof(DeclaredModuleEndpoints))]
    public async Task SimiTenant_Should_NotBeGated_When_EndpointBelongsToADeclaredModule(string moduleId, string path, string role)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await ProvisionSimiModulesAsync(tenantId);
        var client = await CreateClientAsync(tenantId, role);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            $"the simi pack declares {moduleId}, so {path} must reach its handler");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SimiTenant_Should_BeGated_When_EndpointBelongsToFinance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await ProvisionSimiModulesAsync(tenantId);
        var client = await CreateClientAsync(tenantId, "Operations", "Ledger.Read");

        // Act
        var response = await client.GetAsync("/ledger");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the simi pack does not declare finance and nothing it declares hard-depends on it");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("code").GetString().Should().Be(ModuleErrorCodes.Disabled);
        body.GetProperty("moduleId").GetString().Should().Be(ModuleIds.Finance);
    }

    [Fact]
    public async Task SimiTenant_Should_ResolveExactlyTheDeclaredSetPlusCore()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await ProvisionSimiModulesAsync(tenantId);

        // Act
        await using var scope = _factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IModuleEnablementReader>();
        var resolved = await reader.GetAsync(tenantId);

        // Assert
        resolved.Enabled.Should().BeEquivalentTo(
        [
            ModuleIds.PersonalFinance, ModuleIds.Groups, ModuleIds.Documents, ModuleIds.Voice,
            ModuleIds.Ai, ModuleIds.Agents, ModuleIds.Platform, ModuleIds.Ordering,
        ]);
    }

    private async Task ProvisionSimiModulesAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Simi Pack Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            BusinessType = "simi",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();

        var applier = scope.ServiceProvider.GetRequiredService<IConfigPackApplier>();
        var actions = await applier.ApplyModulesAsync(tenantId, "simi", initialProvisioning: true);
        actions.Should().ContainSingle().Which.Should().Contain("simi");
    }

    private async Task<HttpClient> CreateClientAsync(Guid tenantId, string role, string? permission = null)
    {
        var options = TestAuthOptions.Create().WithRoles(role).WithTenant(tenantId);
        if (permission is not null)
        {
            options.WithPermissions(permission);
        }

        var client = await _factory.CreateAuthenticatedClientAsync(options);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}
