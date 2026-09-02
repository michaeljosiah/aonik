using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Endpoints;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 097 §12.1 / acceptance 10 through the real host: a disabled module's agents are absent
/// from <c>GET /ai/agents</c>, refused by the playground and by the by-name configuration endpoints,
/// and present again for a tenant with no rows. Every test uses a fresh tenant id because the reader
/// caches per tenant.
/// </summary>
public class AgentModuleGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string FinanceAgent = "finance-agent";
    private const string CommerceAgent = "commerce-agent";
    private const string PlatformAgent = "platform-agent";

    private readonly CustomWebApplicationFactory _factory;

    public AgentModuleGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ListAgents_Should_OmitFinanceAndCommerceAgents_When_FinanceIsOffForTenant()
    {
        // Arrange — commerce hard-depends on finance, so the closure takes commerce down too.
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");
        await SeedModuleRowAsync(tenantId, ModuleIds.Finance, isEnabled: false);

        // Act
        var response = await client.GetAsync("/ai/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var names = await ReadAgentNamesAsync(response);
        names.Should().NotContain(FinanceAgent).And.NotContain(CommerceAgent);
        names.Should().Contain(PlatformAgent, "Platform is core and its agent is never hidden");
    }

    [Fact]
    public async Task ListAgents_Should_IncludeFinanceAgent_When_TenantHasNoRows()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");

        // Act
        var response = await client.GetAsync("/ai/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var names = await ReadAgentNamesAsync(response);
        names.Should().Contain(FinanceAgent).And.Contain(CommerceAgent).And.Contain(PlatformAgent);
    }

    [Fact]
    public async Task PlaygroundRun_Should_Return403ModuleDisabled_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");
        await SeedModuleRowAsync(tenantId, ModuleIds.Finance, isEnabled: false);

        // Act
        var response = await client.PostAsJsonAsync("/ai/playground/run", new PlaygroundRunRequest
        {
            AgentName = FinanceAgent,
            Messages = [new PlaygroundMessage { Role = "user", Content = "Hello" }],
        });

        // Assert — refused before the SSE stream starts, so it is a real 403, not an error event.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.Finance);
    }

    [Fact]
    public async Task GetAgentConfiguration_Should_Return403ModuleDisabled_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");
        await SeedModuleRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);

        // Act
        var response = await client.GetAsync($"/ai/agents/configurations/{CommerceAgent}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.Commerce);
    }

    [Fact]
    public async Task UpsertAgentConfiguration_Should_Return403ModuleDisabled_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");
        await SeedModuleRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/ai/agents/configurations/{CommerceAgent}",
            new UpsertAgentConfigurationEndpointRequest { InstructionsText = "Sell everything." });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        GetString(await ReadBodyAsync(response), "code").Should().Be(ModuleErrorCodes.Disabled);
    }

    [Fact]
    public async Task DeleteAgentConfiguration_Should_Return403ModuleDisabled_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");
        await SeedModuleRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);

        // Act
        var response = await client.DeleteAsync($"/ai/agents/configurations/{CommerceAgent}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        GetString(await ReadBodyAsync(response), "code").Should().Be(ModuleErrorCodes.Disabled);
    }

    [Fact]
    public async Task GetAgentConfiguration_Should_NotBeDenied_When_TenantHasNoRows()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin");

        // Act
        var response = await client.GetAsync($"/ai/agents/configurations/{CommerceAgent}");

        // Assert — 200 (a seeded row) or 404 (none yet); never the module gate.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task<HttpClient> CreateClientAsync(Guid tenantId, string role)
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles(role).WithTenant(tenantId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task SeedModuleRowAsync(Guid tenantId, string moduleId, bool isEnabled)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        db.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            IsEnabled = isEnabled,
            Source = TenantModuleSource.Explicit,
            Reason = "agent module gate test",
        });
        await db.SaveChangesAsync();
    }

    private static async Task<List<string>> ReadAgentNamesAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ListAgentsResponse>();
        payload.Should().NotBeNull();
        return payload!.Agents.Select(a => a.Name).ToList();
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace("the module gate always writes a typed body");
        return JsonDocument.Parse(json).RootElement;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }

        return null;
    }
}
