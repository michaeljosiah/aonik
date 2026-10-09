using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Aonik.Api.Tests;

public sealed class StripeWebhookEndpointTests : IClassFixture<StripeWebhookEndpointTests.Factory>
{
    private const string Secret = "whsec_fixture_only";
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private readonly Factory _factory;
    public StripeWebhookEndpointTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Webhook_Should_AcceptRealSignatureOnce_WithoutTenantHeaderOrSynchronousFinancialEffects()
    {
        var intent = await SeedAsync();
        using var client = Client();
        var body = Body(intent, "evt_api_duplicate");

        using var first = await PostAsync(client, intent, body, Sign(body));
        using var duplicate = await PostAsync(client, intent, body, Sign(body));

        first.StatusCode.Should().Be(HttpStatusCode.OK, "{0}", string.Join(Environment.NewLine,
            _factory.Logs.Entries.Where(entry => entry.Exception is not null).Select(entry => entry.Exception)));
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadAsStringAsync()).Should().BeEmpty();
        first.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var inbox = (await db.PartnerWebhookEvents.Where(e => e.ConnectorId == intent.ConnectorId).ToListAsync())
            .Should().ContainSingle().Subject;
        inbox.TenantId.Should().Be(intent.TenantId);
        inbox.SignatureValid.Should().BeTrue();
        inbox.ProcessedAt.Should().BeNull();
        inbox.RawPayload.Should().NotContain("private@example.com").And.NotContain(Secret);
        (await db.Set<OutboxMessage>().CountAsync(e => e.TenantId == intent.TenantId
            && e.EventType == typeof(CheckoutPaymentReconciliationRequestedEvent).FullName)).Should().Be(1);
        (await db.Payments.CountAsync(p => p.TenantId == intent.TenantId)).Should().Be(0);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("stale")]
    [InlineData("repeated")]
    public async Task Webhook_Should_RejectUnverifiedRequest_BeforeDurableAcceptance(string failure)
    {
        var intent = await SeedAsync();
        using var client = Client();
        var body = Body(intent, "evt_invalid");
        using var request = new HttpRequestMessage(HttpMethod.Post, Path(intent)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (failure != "missing") request.Headers.TryAddWithoutValidation("Stripe-Signature",
            failure == "invalid" ? "t=1,v1=invalid" : Sign(body, failure == "stale" ? -1000 : 0));
        if (failure == "repeated") request.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(body));

        using var result = await client.SendAsync(request);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().PartnerWebhookEvents
            .CountAsync(e => e.ConnectorId == intent.ConnectorId)).Should().Be(0);
    }

    [Fact]
    public async Task Webhook_Should_BoundBodyAndRejectSignedWrongTenantCorrelation()
    {
        var intent = await SeedAsync();
        using var client = Client();
        using var oversized = await PostAsync(client, intent, new string('x', 256 * 1024 + 1), "t=1,v1=placeholder");
        oversized.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        oversized.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = Body(intent, "evt_wrong_tenant", Guid.NewGuid());

        using var mismatch = await PostAsync(client, intent, body, Sign(body));

        mismatch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().PartnerWebhookEvents
            .CountAsync(e => e.ConnectorId == intent.ConnectorId)).Should().Be(0);
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
    private static string Path(PaymentIntent intent) => $"/integrations/stripe/webhooks/{intent.ConnectorId}";
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, PaymentIntent intent, string body, string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path(intent)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        return await client.SendAsync(request);
    }

    private static string Body(PaymentIntent intent, string eventId, Guid? tenantOverride = null) => JsonSerializer.Serialize(new
    {
        id = eventId, @object = "event", type = "checkout.session.completed", api_version = Stripe.StripeConfiguration.ApiVersion,
        livemode = false, data = new { @object = new { id = intent.ProviderReference, @object = "checkout.session",
            payment_intent = "pi_test", customer_email = "private@example.com", metadata = new
            {
                paymentIntentId = intent.Id.ToString("N"), orderId = intent.OrderId.ToString("N"),
                tenantId = (tenantOverride ?? intent.TenantId).ToString("N"), connectorId = intent.ConnectorId!.Value.ToString("N")
            } } }
    });

    private static string Sign(string body, int offset = 0)
    {
        var timestamp = new DateTimeOffset(Now).ToUnixTimeSeconds() + offset;
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"t={timestamp},v1={Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private async Task<PaymentIntent> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var connector = new Connector { TenantId = tenant, ConnectorType = "stripe-checkout-v1", Status = "Active" };
        var intent = new PaymentIntent { TenantId = tenant, ConnectorId = connector.Id, ProviderCode = "Stripe",
            ProviderAccountId = "acct_fixture", ProviderLiveMode = false, ProviderReference = "cs_" + Guid.NewGuid().ToString("N"),
            OrderId = Guid.NewGuid(), Amount = 10m, Currency = "GBP", Status = "Processing", ProviderRequestStartedAtUtc = Now };
        db.Connectors.Add(connector);
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();
        return intent;
    }

    public sealed class Factory : CustomWebApplicationFactory
    {
        public CapturingLoggerProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStripeConnectorResolver>();
                services.AddScoped<IStripeConnectorResolver, TestResolver>();
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock, FixedClock>();
                services.AddSingleton<ILoggerProvider>(Logs);
            });
        }
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => Now; }
    private sealed class TestResolver(ITenantProvider tenant) : IStripeConnectorResolver
    {
        public Task<StripeConnectorBinding> ResolveSelectedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StripeConnectorBinding> ResolveBoundAsync(Guid connectorId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StripeConnectorBinding { TenantId = tenant.GetCurrentTenantId(), ConnectorId = connectorId,
                ProviderAccountId = "acct_fixture", LiveMode = false, SecretKey = "sk_test_fixture", SigningSecrets = [Secret], ReturnOrigin = "https://shop.example" });
    }
}
