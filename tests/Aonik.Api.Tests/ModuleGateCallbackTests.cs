using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Infrastructure.Persistence;
using Aonik.PersonalFinance.Agents.CodeAct;
using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Codex P1-3 on Spec 097 §11: anonymous provider callbacks resolve their owning tenant from the
/// payload (or from the entity it references) only after <c>ModuleEnablementMiddleware</c> has run —
/// and the middleware deliberately passes a request through when no tenant is resolved. Each such
/// processor therefore re-checks enablement through <see cref="IModuleGate"/> the moment it knows
/// the owning tenant, before it mutates anything. These tests drive the real host.
/// </summary>
/// <remarks>
/// <para>
/// <b>How the partner webhook tests reach the in-processor gate.</b> The request carries an
/// <c>X-Tenant-Id</c> for an unrelated tenant whose Finance module is on, so the HTTP gate lets it in;
/// the payout the payload references belongs to a different tenant whose Finance module is off. The
/// remittance processor locates payouts across tenants, so the only thing standing between the
/// callback and a settlement is the re-check — exactly the bypass the finding describes.
/// </para>
/// <para>
/// <b>Why the Plaid tests assert the observable 403 only.</b> Both Plaid processors look the
/// connection up through a tenant-scoped context whose filter fails closed, so a connection can only
/// ever resolve to the ambient tenant, which the HTTP gate has already checked. The in-processor
/// re-check is therefore defence in depth today and would become load-bearing the day that lookup goes
/// cross-tenant; it is proven at the service level in <c>Aonik.Application.Tests</c>
/// (<c>PersonalAccountLinkServiceTests</c>, <c>AccountLinkServiceTests</c>) with a denying gate.
/// </para>
/// </remarks>
public class ModuleGateCallbackTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModuleGateCallbackTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ─── Partner webhook: /partners/webhooks/{providerCode} ────────────────────

    [Fact]
    public async Task PartnerWebhook_Should_Return403ModuleDisabledAndMutateNothing_When_OwningTenantHasFinanceOff()
    {
        // Arrange — the payout's tenant has Finance off; the tenant the request resolves to does not.
        var ownerTenantId = Guid.NewGuid();
        var callerTenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(ownerTenantId);
        await SeedActiveTenantAsync(callerTenantId);
        await SeedModuleRowAsync(ownerTenantId, ModuleIds.Finance, isEnabled: false);
        var payout = await SeedPayoutAsync(ownerTenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", callerTenantId.ToString());

        // Act
        var response = await client.PostAsync("/partners/webhooks/Simulated", PayoutSucceededBody(payout));

        // Assert — the provider-facing answer
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the payout's tenant has Finance off, so the processor must refuse once it resolves that tenant");
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.Finance);

        // Assert — nothing recorded, nothing settled
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Set<PartnerWebhookEvent>().AcrossTenants().AnyAsync(e => e.ClientReference == payout.ClientReference))
            .Should().BeFalse("no inbox row may be recorded for a module that is off");
        var stored = await db.Set<Payout>().AcrossTenants().SingleAsync(p => p.Id == payout.Id);
        stored.Status.Should().Be("Processing", "the payout must not be touched");
    }

    [Fact]
    public async Task PartnerWebhook_Should_ReachTheProcessorAsBefore_When_OwningTenantHasFinanceOn()
    {
        // Arrange — same shape, Finance on (no row: catalogue default) for the payout's tenant.
        var ownerTenantId = Guid.NewGuid();
        var callerTenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(ownerTenantId);
        await SeedActiveTenantAsync(callerTenantId);
        var payout = await SeedPayoutAsync(ownerTenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", callerTenantId.ToString());

        // Act
        var response = await client.PostAsync("/partners/webhooks/Simulated", PayoutSucceededBody(payout));

        // Assert — acknowledged, and the processor recorded the callback in its inbox (the signature
        // is not valid for this test, so the row is a rejected audit entry, exactly as before the gate).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ack = await response.Content.ReadFromJsonAsync<JsonElement>();
        ack.GetProperty("received").GetBoolean().Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var inbox = await db.Set<PartnerWebhookEvent>().AcrossTenants()
            .SingleOrDefaultAsync(e => e.ClientReference == payout.ClientReference);
        inbox.Should().NotBeNull("with Finance on the callback must reach the processor and be recorded");
        inbox!.SignatureValid.Should().BeFalse();
    }

    // ─── Plaid webhook: /admin/accounts/webhooks/plaid ─────────────────────────

    [Fact]
    public async Task AdminPlaidWebhook_Should_Return403ModuleDisabledAndMutateNothing_When_OwningTenantHasPersonalFinanceOff()
    {
        // Arrange — the /admin prefix skips tenant validation, so this is the one Plaid sink an anonymous
        // caller reaches with nothing but a header. The tenant it names has Personal Finance off.
        var tenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(tenantId);
        await SeedModuleRowAsync(tenantId, ModuleIds.PersonalFinance, isEnabled: false);
        var connection = await SeedAccountConnectionAsync(tenantId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Act
        var response = await client.PostAsJsonAsync("/admin/accounts/webhooks/plaid", new PlaidAccountWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = "USER_PERMISSION_REVOKED",
            ItemId = connection.ProviderConnectionReference,
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadBodyAsync(response);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.PersonalFinance);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var stored = await db.Set<AccountConnection>().AcrossTenants().SingleAsync(c => c.Id == connection.Id);
        stored.Status.Should().Be("Connected", "a revoke for a tenant with the module off must not disconnect anything");
        stored.DisconnectedAt.Should().BeNull();
        stored.LastWebhookReceivedAt.Should().BeNull();
    }

    // ─── CodeAct sandbox callback: /ai/codeact/call-tool/{nonce} ──────────────

    [Fact]
    public async Task CodeActCallback_Should_Return403ModuleDisabled_When_NonceTenantHasPersonalFinanceOff()
    {
        // Arrange — the nonce, not a header, is where this anonymous request carries its tenant.
        using var host = CreateHostWithNonceSigningKey();
        var tenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(host.Services, tenantId);
        await SeedModuleRowAsync(host.Services, tenantId, ModuleIds.PersonalFinance, isEnabled: false);
        var nonce = MintNonce(host.Services, tenantId, "pf_get_merchant_history");

        var client = host.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/ai/codeact/call-tool/{nonce}",
            new { name = "pf_get_merchant_history", args = new { } });

        // Assert — refused with the bridge's own envelope carrying the typed module code
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadBodyAsync(response);
        GetString(body, "status").Should().Be("error");
        GetString(body.GetProperty("error"), "code").Should().Be(ModuleErrorCodes.Disabled);
    }

    [Fact]
    public async Task CodeActCallback_Should_ProceedPastTheGate_When_NonceTenantHasPersonalFinanceOn()
    {
        // Arrange — Personal Finance on; the tool name is deliberately outside the nonce's whitelist so the
        // next check in the pipeline (the whitelist) is what answers, proving the gate let it through.
        using var host = CreateHostWithNonceSigningKey();
        var tenantId = Guid.NewGuid();
        await SeedActiveTenantAsync(host.Services, tenantId);
        var nonce = MintNonce(host.Services, tenantId, "pf_get_merchant_history");

        var client = host.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/ai/codeact/call-tool/{nonce}",
            new { name = "pf_not_whitelisted", args = new { } });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadBodyAsync(response);
        GetString(body.GetProperty("error"), "code").Should().Be("tool_not_in_whitelist",
            "with the module on, the request must reach the whitelist check that follows the gate");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private WebApplicationFactory<Program> CreateHostWithNonceSigningKey()
        => _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:CodeAct:NonceSigningKey"] = new string('a', 64),
            })));

    private static string MintNonce(IServiceProvider services, Guid tenantId, string allowedTool)
    {
        var nonceService = services.GetRequiredService<CodeActCallbackNonceService>();
        return nonceService.Issue(
            new CodeActSandboxContext("pf-insights", Guid.NewGuid().ToString("N"), tenantId, Guid.NewGuid()),
            new HashSet<string>(StringComparer.Ordinal) { allowedTool },
            maxCallbacks: 5,
            ttl: TimeSpan.FromMinutes(5));
    }

    private static StringContent PayoutSucceededBody(Payout payout)
    {
        var body =
            $"{{\"category\":\"Payout\",\"event\":\"payout.succeeded\"," +
            $"\"clientReference\":\"{payout.ClientReference}\",\"providerReference\":\"{payout.ProviderReference}\"," +
            $"\"status\":\"Succeeded\",\"code\":\"00\",\"message\":\"ok\"}}";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.Add("x-simulated-signature", "not-the-configured-secret");
        return content;
    }

    private Task SeedActiveTenantAsync(Guid tenantId) => SeedActiveTenantAsync(_factory.Services, tenantId);

    private static async Task SeedActiveTenantAsync(IServiceProvider services, Guid tenantId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        if (await db.Tenants.AnyAsync(t => t.Id == tenantId)) return;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Module Gate Callback Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
    }

    private Task SeedModuleRowAsync(Guid tenantId, string moduleId, bool isEnabled)
        => SeedModuleRowAsync(_factory.Services, tenantId, moduleId, isEnabled);

    private static async Task SeedModuleRowAsync(IServiceProvider services, Guid tenantId, string moduleId, bool isEnabled)
    {
        await using var scope = services.CreateAsyncScope();
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
            Reason = "module gate callback test",
        });
        await db.SaveChangesAsync();
    }

    private async Task<Payout> SeedPayoutAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 990m,
            Currency = "NGN",
            DebitCurrency = "NGN",
            ClientReference = $"REM-{Guid.NewGuid():N}",
            ProviderReference = $"pr_{Guid.NewGuid():N}",
            DestinationType = "Bank",
            Status = "Processing",
        };
        db.Set<Payout>().Add(payout);
        await db.SaveChangesAsync();
        return payout;
    }

    private async Task<AccountConnection> SeedAccountConnectionAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var connection = new AccountConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedByUserId = Guid.NewGuid(),
            Provider = "Plaid",
            ProviderConnectionReference = $"item-{Guid.NewGuid():N}",
            InstitutionName = "Test Bank",
            Status = "Connected",
            ConsentStatus = "Granted",
            SecretReference = "vault://test",
        };
        db.Set<AccountConnection>().Add(connection);
        await db.SaveChangesAsync();
        return connection;
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace("a refusal always carries a typed body");
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
