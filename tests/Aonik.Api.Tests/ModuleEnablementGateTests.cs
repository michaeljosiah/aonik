using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

using Aonik.Api.Middleware;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Api.Modules;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FastEndpoints;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 097 §11 / acceptance 8 and 9: for a tenant with a module off, any endpoint in that module's
/// assembly answers <c>403 { code: "module.disabled", moduleId }</c>; the same request is not denied
/// once the module is on (or has no row); Platform endpoints are unaffected; anonymous storefront
/// endpoints that resolve the tenant by header are gated the same way. Every test uses a fresh tenant
/// id because the reader caches per tenant for five minutes.
/// </summary>
public class ModuleEnablementGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModuleEnablementGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    // One simple, authenticated GET per non-core module assembly. The role is the least privileged
    // one the endpoint's policy accepts; the permission (when any) is what the service behind it
    // checks, so the "module on" case reaches the handler instead of a permission 403.
    public static TheoryData<string, string, string, string?> ModuleEndpoints => new()
    {
        { ModuleIds.Finance, "/ledger", "Operations", "Ledger.Read" },
        { ModuleIds.Commerce, "/commerce/admin/products", "Operations", null },
        { ModuleIds.Subscriptions, "/subscriptions/admin/meters", "Operations", null },
        { ModuleIds.PersonalFinance, "/personal-finance/categories", "PersonalUser", null },
        { ModuleIds.Voice, "/tenant/settings/voice/recipes", "TenantAdmin", null },
        { ModuleIds.Documents, "/documents", "Operations", null },
    };

    // ─── HTTP: one endpoint per non-core module ──────────────────────────────────

    [Theory]
    [MemberData(nameof(ModuleEndpoints))]
    public async Task ModuleEndpoint_Should_Return403ModuleDisabled_When_ModuleIsOffForTenant(
        string moduleId, string path, string role, string? permission)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, role, permission);
        await SeedModuleRowAsync(tenantId, moduleId, isEnabled: false);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"{path} lives in the {moduleId} assembly, which is off for this tenant");
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(moduleId);
        GetString(body, "error").Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(ModuleEndpoints))]
    public async Task ModuleEndpoint_Should_NotBeDenied_When_ModuleIsExplicitlyOnForTenant(
        string moduleId, string path, string role, string? permission)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, role, permission);
        await SeedModuleRowAsync(tenantId, moduleId, isEnabled: true);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            $"{moduleId} is explicitly on, so the gate must let {path} through to its handler");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Theory]
    [MemberData(nameof(ModuleEndpoints))]
    public async Task ModuleEndpoint_Should_NotBeDenied_When_TenantHasNoModuleRows(
        string moduleId, string path, string role, string? permission)
    {
        // Arrange — no rows at all resolves to the catalogue defaults (everything on).
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, role, permission);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            $"a tenant with no rows keeps the catalogue default for {moduleId}, so {path} must not be gated");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ModuleEndpoint_Should_AnswerNormallyAgain_When_ModuleIsToggledBackOn()
    {
        // Arrange — the acceptance criterion says "without a restart": flip the same row through
        // the production write path (PUT /admin/tenants/{id}/modules), which invalidates the
        // reader's cache itself.
        var tenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(tenantId);
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        await SeedModuleRowAsync(tenantId, ModuleIds.Documents, isEnabled: false);

        var denied = await client.GetAsync("/documents");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Act
        var hostAdmin = await CreatePlatformAdminClientAsync(tenantId);
        var toggled = await hostAdmin.PutAsJsonAsync(
            $"/admin/tenants/{tenantId}/modules",
            new TenantModuleUpdateRequest([new TenantModuleToggleRequest(ModuleIds.Documents, true, "module gate test")]));
        toggled.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await client.GetAsync("/documents");

        // Assert
        restored.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        restored.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ─── HTTP: the Voice WebSocket map (RequestDelegate, no FastEndpoints definition) ─

    [Fact]
    public async Task VoiceWebSocket_Should_Return403ModuleDisabled_When_VoiceIsOffForTenant()
    {
        // Arrange — the gate runs before the handler would demand a WebSocket upgrade, so a plain
        // GET is enough to prove the socket is refused for a tenant with Voice off.
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "PersonalUser", permission: null);
        await SeedModuleRowAsync(tenantId, ModuleIds.Voice, isEnabled: false);

        // Act
        var response = await client.GetAsync("/ai/voice");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the Voice WebSocket is mapped straight from a RequestDelegate and must still be gated");
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.Voice);
    }

    [Fact]
    public async Task VoiceWebSocket_Should_ReachTheHandler_When_VoiceIsOnForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "PersonalUser", permission: null);

        // Act
        var response = await client.GetAsync("/ai/voice");

        // Assert — the Voice handler answers a plain GET with its own "upgrade required" 400.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the handler ran and demanded a WebSocket upgrade");
    }

    // ─── HTTP: Platform (core) is never gated ────────────────────────────────────

    [Fact]
    public async Task PlatformEndpoint_Should_BeUnaffected_When_FinanceIsOffForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, "TenantAdmin", "Ledger.Read");
        await SeedModuleRowAsync(tenantId, ModuleIds.Finance, isEnabled: false);

        // Act
        var financeResponse = await client.GetAsync("/ledger");
        var platformResponse = await client.GetAsync("/tenant/settings");

        // Assert — the toggle is live (Finance denied by the gate, not by a permission) and
        // Platform still answers.
        financeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        GetString(await ReadBodyAsync(financeResponse), "code").Should().Be(ModuleErrorCodes.Disabled);
        platformResponse.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "Platform is core and its endpoints must never be gated");
        platformResponse.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ─── HTTP: anonymous storefront resolves the tenant by header and IS gated ───

    [Fact]
    public async Task AnonymousStorefrontEndpoint_Should_Return403ModuleDisabled_When_CommerceIsOffForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(tenantId);
        await SeedModuleRowAsync(tenantId, ModuleIds.Commerce, isEnabled: false);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync("/commerce/catalog/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a storefront whose tenant has Commerce off must be denied even though the caller is anonymous");
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.Commerce);
    }

    [Fact]
    public async Task AnonymousStorefrontEndpoint_Should_NotBeDenied_When_CommerceIsOnForTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(tenantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.GetAsync("/commerce/catalog/products");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ─── Middleware unit tests (no host) ────────────────────────────────────────

    [Fact]
    public async Task Middleware_Should_SkipGate_When_EndpointCarriesModuleGateExempt()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var reader = new FakeModuleReader(tenantId, enabled: new HashSet<string>(StringComparer.Ordinal));
        var nextCalled = false;
        var context = BuildContext(
            tenantId,
            reader,
            endpointMetadata: [CommerceHandler, new ModuleGateExempt()]);
        var middleware = new ModuleEnablementMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<ModuleEnablementMiddleware>.Instance);

        // Act
        var act = () => middleware.InvokeAsync(context);

        // Assert
        await act.Should().NotThrowAsync();
        nextCalled.Should().BeTrue();
        reader.Calls.Should().Be(0, "an exempt endpoint must not even cost a reader lookup");
    }

    [Fact]
    public async Task Middleware_Should_ThrowModuleDisabled_When_ModuleIsOffAndEndpointIsNotExempt()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var reader = new FakeModuleReader(tenantId, enabled: new HashSet<string>(StringComparer.Ordinal));
        var nextCalled = false;
        var context = BuildContext(tenantId, reader, endpointMetadata: [CommerceHandler]);
        var middleware = new ModuleEnablementMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<ModuleEnablementMiddleware>.Instance);

        // Act
        var act = () => middleware.InvokeAsync(context);

        // Assert
        (await act.Should().ThrowAsync<ModuleDisabledException>())
            .Which.ModuleId.Should().Be(ModuleIds.Commerce);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Middleware_Should_SkipGate_When_TenantIsNotResolved()
    {
        // Arrange
        var reader = new FakeModuleReader(Guid.NewGuid(), enabled: new HashSet<string>(StringComparer.Ordinal));
        var nextCalled = false;
        var context = BuildContext(tenantId: null, reader, endpointMetadata: [CommerceHandler]);
        var middleware = new ModuleEnablementMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<ModuleEnablementMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Middleware_Should_SkipGate_When_EndpointBelongsToCoreModule()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var reader = new FakeModuleReader(tenantId, enabled: new HashSet<string>(StringComparer.Ordinal));
        var nextCalled = false;
        var context = BuildContext(tenantId, reader, endpointMetadata: [PlatformHandler]);
        var middleware = new ModuleEnablementMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<ModuleEnablementMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        reader.Calls.Should().Be(0, "core modules are never off, so no lookup is needed");
    }

    [Fact]
    public async Task Middleware_Should_SkipGate_When_NoEndpointIsRouted()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var reader = new FakeModuleReader(tenantId, enabled: new HashSet<string>(StringComparer.Ordinal));
        var nextCalled = false;
        var context = BuildContext(tenantId, reader, endpointMetadata: null);
        var middleware = new ModuleEnablementMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<ModuleEnablementMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public void ResolveModuleId_Should_UseEndpointDefinition_When_EndpointIsFastEndpoints()
    {
        // Arrange — FastEndpoints attaches its EndpointDefinition; the gate reads EndpointType from it.
        var definition = new EndpointDefinition(
            typeof(Aonik.Commerce.Endpoints.Public.Catalog.ListCatalogProductsEndpoint),
            typeof(EmptyRequest),
            typeof(object));
        var endpoint = new Endpoint(null, new EndpointMetadataCollection(definition), "fe");

        // Act
        var moduleId = ModuleEnablementMiddleware.ResolveModuleId(endpoint);

        // Assert
        moduleId.Should().Be(ModuleIds.Commerce);
    }

    [Fact]
    public void ResolveModuleId_Should_UseHandlerDeclaringType_When_EndpointIsMinimalApi()
    {
        // Arrange — minimal APIs attach the handler MethodInfo; the Voice WebSocket is mapped this way.
        // The handler class is internal to Voice, so locate it through the public mapping extension.
        var handlerType = typeof(Aonik.Voice.Endpoints.VoiceWebSocketEndpointExtensions).Assembly
            .GetType("Aonik.Voice.Endpoints.VoiceWebSocketEndpoint");
        handlerType.Should().NotBeNull("MapAonikVoiceEndpoints maps VoiceWebSocketEndpoint.HandleAsync");
        var handler = handlerType!.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        handler.Should().NotBeNull();
        var endpoint = new Endpoint(null, new EndpointMetadataCollection(handler!), "minimal");

        // Act
        var moduleId = ModuleEnablementMiddleware.ResolveModuleId(endpoint);

        // Assert
        moduleId.Should().Be(ModuleIds.Voice);
    }

    [Fact]
    public void ResolveModuleId_Should_UseRequestDelegateDeclaringType_When_EndpointCarriesNoMethodInfo()
    {
        // Arrange — MapGet(pattern, RequestDelegate) attaches neither an EndpointDefinition nor a
        // MethodInfo; the only thing left is the delegate itself. Build one from the real Voice handler.
        var handlerType = typeof(Aonik.Voice.Endpoints.VoiceWebSocketEndpointExtensions).Assembly
            .GetType("Aonik.Voice.Endpoints.VoiceWebSocketEndpoint")!;
        var handler = handlerType.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        var requestDelegate = (RequestDelegate)Delegate.CreateDelegate(typeof(RequestDelegate), handler);
        var endpoint = new Endpoint(requestDelegate, new EndpointMetadataCollection(), "request-delegate");

        // Act
        var moduleId = ModuleEnablementMiddleware.ResolveModuleId(endpoint);

        // Assert
        moduleId.Should().Be(ModuleIds.Voice);
    }

    [Theory]
    [InlineData("/ai/voice", ModuleIds.Voice)]
    [InlineData("/admin/notifications/stream", ModuleIds.Platform)]
    [InlineData("/ai/playground/review", ModuleIds.Agents)]
    public void ResolveModuleId_Should_ResolveTheRealMappedEndpoint_When_ItIsMappedFromARequestDelegate(string route, string expectedModuleId)
    {
        // Arrange — the endpoints exactly as Program.cs mapped them in the running host.
        var endpoint = FindRouteEndpoint(route);

        // Act
        var moduleId = ModuleEnablementMiddleware.ResolveModuleId(endpoint);

        // Assert
        moduleId.Should().Be(expectedModuleId, $"{route} is mapped from a RequestDelegate in the {expectedModuleId} assembly");
    }

    [Fact]
    public void EveryRoutedEndpoint_Should_HaveAResolvableOwningType_So_NoMapCanSilentlyBypassTheGate()
    {
        // Arrange — a future map whose owning type the gate cannot see would be invisible to the
        // module gate; this fails the build instead. Framework-mapped endpoints (health, OpenAPI)
        // resolve to a framework type and fall through the attribute check, which is fine.
        var routed = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RequestDelegate is not null)
            .ToList();
        routed.Should().NotBeEmpty();

        // Act
        var unresolvable = routed
            .Where(endpoint => ModuleEnablementMiddleware.ResolveOwningType(endpoint) is null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        // Assert
        unresolvable.Should().BeEmpty("every routed endpoint must expose an owning type so the module gate can classify it");
    }

    [Fact]
    public void ResolveModuleId_Should_ReturnNull_When_HandlerAssemblyHasNoModuleAttribute()
    {
        // Arrange — this test assembly carries no AonikModuleAttribute, like Api itself.
        var handler = typeof(ModuleEnablementGateTests).GetMethod(nameof(UnattributedHandler),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var endpoint = new Endpoint(null, new EndpointMetadataCollection(handler), "host");

        // Act
        var moduleId = ModuleEnablementMiddleware.ResolveModuleId(endpoint);

        // Assert
        moduleId.Should().BeNull();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private RouteEndpoint FindRouteEndpoint(string route)
    {
        var endpoint = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(candidate => string.Equals(candidate.RoutePattern.RawText, route, StringComparison.OrdinalIgnoreCase));
        endpoint.Should().NotBeNull($"Program.cs maps {route}");
        return endpoint!;
    }

    private Task<HttpClient> CreateClientAsync(Guid tenantId, string role, string? permission)
    {
        var options = TestAuthOptions.Create().WithRoles(role).WithTenant(tenantId);
        if (permission is not null)
        {
            options.WithPermissions(permission);
        }

        return _factory.CreateAuthenticatedClientAsync(options);
    }

    /// <summary>A host admin (PlatformAdmin) on <paramref name="tenantId"/>: the only caller that may change module state.</summary>
    private async Task<HttpClient> CreatePlatformAdminClientAsync(Guid tenantId)
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithTenant(tenantId)
                .WithRoles("PlatformAdmin")
                .WithClaims(new Claim("roles", "Aonik.PlatformAdmin")));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private static MethodInfo CommerceHandler =>
        typeof(Aonik.Commerce.Endpoints.Public.Catalog.ListCatalogProductsEndpoint)
            .GetMethod(nameof(BaseEndpoint.Configure), BindingFlags.Public | BindingFlags.Instance)!;

    private static MethodInfo PlatformHandler =>
        typeof(Aonik.Platform.Endpoints.Tenant.Settings.GetTenantSettingsEndpoint)
            .GetMethod(nameof(BaseEndpoint.Configure), BindingFlags.Public | BindingFlags.Instance)!;

    private static Task UnattributedHandler(HttpContext _) => Task.CompletedTask;

    private static DefaultHttpContext BuildContext(
        Guid? tenantId,
        FakeModuleReader reader,
        IReadOnlyList<object>? endpointMetadata)
    {
        var services = new ServiceCollection()
            .AddSingleton<ITenantProvider>(new FakeTenantProvider(tenantId))
            .AddSingleton<IModuleEnablementReader>(reader)
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        if (endpointMetadata is not null)
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(endpointMetadata), "test"));
        }

        return context;
    }

    private async Task SeedActiveTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        if (await db.Tenants.AnyAsync(t => t.Id == tenantId)) return;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Module Gate Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedModuleRowAsync(Guid tenantId, string moduleId, bool isEnabled)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var row = await db.TenantModules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ModuleId == moduleId);

        if (row is null)
        {
            db.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ModuleId = moduleId,
                IsEnabled = isEnabled,
                Source = TenantModuleSource.Explicit,
                Reason = "module gate test",
            });
        }
        else
        {
            row.IsEnabled = isEnabled;
        }

        await db.SaveChangesAsync();
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

    private sealed class FakeTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId()
            => tenantId ?? throw new InvalidOperationException("Tenant context not available");

        public bool TryGetCurrentTenantId(out Guid resolved)
        {
            resolved = tenantId ?? Guid.Empty;
            return tenantId.HasValue;
        }
    }

    private sealed class FakeModuleReader(Guid tenantId, IReadOnlySet<string> enabled) : IModuleEnablementReader
    {
        public int Calls { get; private set; }

        public Task<ModuleEnablementSet> GetAsync(Guid requestedTenantId, CancellationToken ct = default)
        {
            Calls++;
            var set = ModuleCatalog.CoreIds.Concat(enabled).ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new ModuleEnablementSet(requestedTenantId == tenantId ? tenantId : requestedTenantId, set));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(tenantIds.Distinct().ToList());
    }
}
