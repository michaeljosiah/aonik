using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Services.Remittance;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aonik.Api.Tests;

public sealed class ReceiptEmailDeliveryEndpointTests(ReceiptDeliveryFactory factory) : IClassFixture<ReceiptDeliveryFactory>
{
    [Fact]
    public async Task Validation_Should_RequireTheExpectedSecretSubscriptionAndTopic_BeforeEchoing()
    {
        using var client = factory.CreateClient();
        var validation = new[] { new { id = "validation", topic = ReceiptDeliveryFactory.Topic, eventType = "Microsoft.EventGrid.SubscriptionValidationEvent", data = new { validationCode = "validation-code", validationUrl = "https://untrusted.invalid/do-not-fetch" } } };
        (await client.PostAsJsonAsync(ReceiptDeliveryFactory.Path, validation)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Authorize(client, "SubscriptionValidation");
        var valid = await client.PostAsJsonAsync(ReceiptDeliveryFactory.Path, validation);
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        (await valid.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["validationResponse"].Should().Be("validation-code");
        client.DefaultRequestHeaders.Remove("X-Aonik-Integration-Key");
        client.DefaultRequestHeaders.Add("X-Aonik-Integration-Key", "wrong");
        (await client.PostAsJsonAsync(ReceiptDeliveryFactory.Path, validation)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Notification_Should_ResolveOnlyTheAuthenticatedTenant_AndForwardDeliveryData()
    {
        using var client = factory.CreateClient();
        Authorize(client);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var messageId = Guid.NewGuid();
        var before = factory.Reports.Count;
        var response = await client.PostAsJsonAsync(ReceiptDeliveryFactory.Path, new[] { Event(messageId, "Delivered"), Event(Guid.NewGuid(), "Bounced") });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var received = factory.Reports.Skip(before).ToArray();
        received.Should().HaveCount(2);
        received.Should().OnlyContain(item => item.TenantId == ReceiptDeliveryFactory.TenantId);
        received[0].Report.MessageId.Should().Be(messageId);
        received.Select(item => item.Report.Status).Should().Equal("Delivered", "Bounced");
        (await client.PostAsJsonAsync($"/integrations/email/azure/{Guid.NewGuid()}/events", new[] { Event(messageId) })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("topic")]
    [InlineData("sender")]
    [InlineData("timestamp")]
    public async Task Notification_Should_RejectSourceOrPayloadMismatch_BeforeProcessingTheBatch(string invalid)
    {
        using var client = factory.CreateClient();
        Authorize(client);
        var data = Event(Guid.NewGuid(), invalid: invalid);
        var before = factory.Reports.Count;
        (await client.PostAsJsonAsync(ReceiptDeliveryFactory.Path, new[] { Event(Guid.NewGuid()), data })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.Reports.Count.Should().Be(before);
    }

    [Fact]
    public async Task Notification_Should_RejectUnknownSubscriptionInvalidJsonAndOversizedBodies()
    {
        using var client = factory.CreateClient();
        Authorize(client);
        client.DefaultRequestHeaders.Remove("aeg-subscription-name");
        client.DefaultRequestHeaders.Add("aeg-subscription-name", "other-subscription");
        (await client.PostAsJsonAsync(ReceiptDeliveryFactory.Path, new[] { Event(Guid.NewGuid()) })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Remove("aeg-subscription-name");
        client.DefaultRequestHeaders.Add("aeg-subscription-name", "receipt-delivery");
        (await client.PostAsync(ReceiptDeliveryFactory.Path, new StringContent("{", System.Text.Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync(ReceiptDeliveryFactory.Path, new StringContent(new string('x', 270000), System.Text.Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private static void Authorize(HttpClient client, string eventType = "Notification")
    {
        client.DefaultRequestHeaders.Add("X-Aonik-Integration-Key", ReceiptDeliveryFactory.Secret);
        client.DefaultRequestHeaders.Add("aeg-subscription-name", "receipt-delivery");
        client.DefaultRequestHeaders.Add("aeg-event-type", eventType);
    }

    private static object Event(Guid id, string status = "Delivered", string? invalid = null) => new
    {
        id = Guid.NewGuid().ToString(), topic = invalid == "topic" ? "/unexpected/resource" : ReceiptDeliveryFactory.Topic,
        eventType = "Microsoft.Communication.EmailDeliveryReportReceived",
        data = new { messageId = id.ToString(), sender = invalid == "sender" ? "foreign@example.test" : "receipts@example.test",
            recipient = "nadia@example.test", status, deliveryAttemptTimeStamp = invalid == "timestamp" ? "not-a-time" : DateTimeOffset.UtcNow.ToString("O") }
    };
}

public sealed class ReceiptDeliveryFactory : CustomWebApplicationFactory
{
    public static readonly Guid TenantId = Guid.Parse("109906ea-5a56-4aae-a3d3-16c10d011111");
    public const string Topic = "/subscriptions/demo/resourceGroups/demo/providers/Microsoft.Communication/communicationServices/demo";
    public const string Secret = "fictional-test-key-for-receipt-events-123456";
    public static string Path => $"/integrations/email/azure/{TenantId}/events";
    public ConcurrentQueue<(Guid? TenantId, ReceiptEmailDeliveryReport Report)> Reports { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<ReceiptEmailDeliveryOptions>(options => options.Subscriptions = [new()
            {
                TenantId = TenantId, Topic = Topic, SubscriptionName = "receipt-delivery", SenderAddress = "receipts@example.test", SharedSecret = Secret
            }]);
            services.RemoveAll<IReceiptEmailDeliveryReceiver>();
            services.AddScoped<IReceiptEmailDeliveryReceiver>(sp => new RecordingReceiver(sp.GetRequiredService<ITenantContext>(), Reports));
        });
    }

    private sealed class RecordingReceiver(ITenantContext tenant, ConcurrentQueue<(Guid?, ReceiptEmailDeliveryReport)> reports) : IReceiptEmailDeliveryReceiver
    {
        public Task RecordAsync(ReceiptEmailDeliveryReport report, CancellationToken cancellationToken = default)
        {
            reports.Enqueue((tenant.TenantId, report));
            return Task.CompletedTask;
        }
    }
}
