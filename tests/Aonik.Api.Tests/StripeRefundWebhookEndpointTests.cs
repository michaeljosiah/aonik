using System.Net;
using System.Text.Json;

using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public sealed partial class StripeWebhookEndpointTests
{
    [Fact]
    public async Task RefundWebhook_Should_DeduplicateSignedNotificationAndEnqueueOnlyRefundRecovery()
    {
        var (intent, refund) = await SeedRefundAsync();
        using var client = Client();
        var body = RefundBody(intent, refund, "evt_refund_once");

        using var first = await PostAsync(client, intent, body, Sign(body));
        using var repeat = await PostAsync(client, intent, body, Sign(body));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var inbox = (await db.PartnerWebhookEvents.Where(x => x.ConnectorId == intent.ConnectorId).ToListAsync())
            .Should().ContainSingle().Subject;
        inbox.Category.Should().Be("Refund");
        inbox.ProcessingStatus.Should().Be("Received");
        inbox.ClientReference.Should().Be(refund.Id.ToString("N"));
        inbox.ProviderReference.Should().Be("re_local");
        inbox.RawPayload.Should().NotContain("private@example.test").And.NotContain("secret_do_not_store");
        var message = (await db.Set<OutboxMessage>().Where(x => x.TenantId == intent.TenantId).ToListAsync())
            .Should().ContainSingle().Subject;
        message.EventType.Should().Be(typeof(RefundReconciliationRequestedEvent).FullName);
        var requested = JsonSerializer.Deserialize<RefundReconciliationRequestedEvent>(message.Payload, OutboxSerialization.Options)!;
        requested.RefundId.Should().Be(refund.Id);
        requested.WebhookEventId.Should().Be(inbox.Id);
        (await db.Refunds.SingleAsync(x => x.Id == refund.Id)).EffectsAppliedAtUtc.Should().BeNull();
        (await db.Payments.CountAsync(x => x.TenantId == intent.TenantId)).Should().Be(0);
        scope.ServiceProvider.GetServices<IEventHandler<RefundReconciliationRequestedEvent>>()
            .Should().ContainSingle(x => x.GetType().Name == "RefundReconciliationRequestedHandler");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("intent")]
    [InlineData("reference")]
    public async Task RefundWebhook_Should_RejectWrongLocalCorrelationWithoutDurableAcceptance(string fault)
    {
        var (intent, refund) = await SeedRefundAsync();
        if (fault == "reference")
        {
            using var update = _factory.Services.CreateScope();
            update.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
            var db = update.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Refunds.SingleAsync(x => x.Id == refund.Id)).ProviderReference = "re_different";
            await db.SaveChangesAsync();
        }
        var body = RefundBody(intent, refund, "evt_refund_bad", tenantOverride: fault == "tenant" ? Guid.NewGuid() : null,
            providerIntentOverride: fault == "intent" ? "pi_other" : null);
        using var client = Client();

        using var result = await PostAsync(client, intent, body, Sign(body));

        result.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().PartnerWebhookEvents
            .CountAsync(x => x.ConnectorId == intent.ConnectorId)).Should().Be(0);
    }

    [Fact]
    public async Task RefundWebhook_Should_KeepDashboardRefundVisibleWithoutInventingAnAuthorizedRefund()
    {
        var (intent, _) = await SeedRefundAsync();
        var body = RefundBody(intent, null, "evt_dashboard");
        using var client = Client();

        using var result = await PostAsync(client, intent, body, Sign(body));

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var inbox = await db.PartnerWebhookEvents.SingleAsync(x => x.ConnectorId == intent.ConnectorId);
        inbox.Category.Should().Be("Refund");
        inbox.ProcessingStatus.Should().Be("NeedsReconciliation");
        inbox.ClientReference.Should().Be(intent.Id.ToString("N"));
        inbox.ProviderReference.Should().Be("re_dashboard");
        inbox.ProcessedAt.Should().BeNull();
        (await db.Set<OutboxMessage>().CountAsync(x => x.TenantId == intent.TenantId)).Should().Be(0);
        (await db.Refunds.CountAsync(x => x.TenantId == intent.TenantId)).Should().Be(1, "only the explicitly seeded request exists");
    }

    [Fact]
    public async Task RefundWebhook_Should_QueueOutOfOrderNotificationsForFreshReadWithoutChangingMoney()
    {
        var (intent, refund) = await SeedRefundAsync();
        using var client = Client();
        var success = RefundBody(intent, refund, "evt_refund_success", status: "succeeded");
        var earlier = RefundBody(intent, refund, "evt_refund_earlier", status: "pending");
        var lateFailure = RefundBody(intent, refund, "evt_refund_late_failure", status: "failed", eventType: "refund.failed");

        using var first = await PostAsync(client, intent, success, Sign(success));
        using var second = await PostAsync(client, intent, earlier, Sign(earlier));
        using var third = await PostAsync(client, intent, lateFailure, Sign(lateFailure));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await db.Set<OutboxMessage>().CountAsync(x => x.TenantId == intent.TenantId
            && x.EventType == typeof(RefundReconciliationRequestedEvent).FullName)).Should().Be(3);
        (await db.Refunds.SingleAsync(x => x.Id == refund.Id)).Status.Should().Be("Requested");
    }

    private async Task<(PaymentIntent Intent, Refund Refund)> SeedRefundAsync()
    {
        var intent = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var saved = await db.PaymentIntents.SingleAsync(x => x.Id == intent.Id);
        saved.ProviderPaymentIntentReference = "pi_" + Guid.NewGuid().ToString("N");
        saved.Status = "Captured";
        intent.ProviderPaymentIntentReference = saved.ProviderPaymentIntentReference;
        var refund = new Refund
        {
            TenantId = intent.TenantId, PaymentIntentId = intent.Id, ConnectorId = intent.ConnectorId,
            Amount = 5, Currency = "GBP", Status = "Requested", Reason = "Customer request",
        };
        db.Refunds.Add(refund);
        await db.SaveChangesAsync();
        return (intent, refund);
    }

    private static string RefundBody(PaymentIntent intent, Refund? refund, string eventId, Guid? tenantOverride = null,
        string? providerIntentOverride = null, string status = "pending", string eventType = "refund.updated")
        => JsonSerializer.Serialize(new
        {
            id = eventId, @object = "event", type = eventType, api_version = Stripe.StripeConfiguration.ApiVersion,
            livemode = false, data = new
            {
                @object = new
                {
                    @object = "refund", id = refund is null ? "re_dashboard" : "re_local", amount = 500, currency = "gbp", status,
                    payment_intent = providerIntentOverride ?? intent.ProviderPaymentIntentReference,
                    description = "secret_do_not_store", instructions_email = "private@example.test",
                    metadata = refund is null ? new Dictionary<string, string>() : new Dictionary<string, string>
                    {
                        ["tenantId"] = (tenantOverride ?? intent.TenantId).ToString("N"),
                        ["connectorId"] = intent.ConnectorId!.Value.ToString("N"), ["orderId"] = intent.OrderId.ToString("N"),
                        ["paymentIntentId"] = intent.Id.ToString("N"), ["refundId"] = refund.Id.ToString("N"),
                    },
                },
            },
        });
}
