using System.Diagnostics;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.Payments;

internal sealed class StripeWebhookService(
    FinanceDbContext db,
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<StripeWebhookService> logger) : IStripeWebhookService
{
    public async Task AcceptAsync(Guid connectorId, string rawBody, string signature, CancellationToken cancellationToken = default)
    {
        using var activity = FinanceActivitySource.Source.StartActivity("stripe.webhook.accept");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Webhook);
        // This global read locates ONLY the requested connector. Caller tenant headers are not authority.
        var connector = await db.Connectors.AcrossTenants().AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == connectorId && c.ConnectorType == "stripe-checkout-v1", cancellationToken)
            ?? throw new NotFoundException("Stripe webhook connector was not found.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = connector.TenantId;
        tenantContext.ResolutionSource = "verified-stripe-connector";
        var binding = await scope.ServiceProvider.GetRequiredService<IStripeConnectorResolver>()
            .ResolveBoundAsync(connectorId, cancellationToken);
        var verified = scope.ServiceProvider.GetRequiredService<IStripeWebhookVerifier>()
            .Verify(rawBody, signature, binding.SigningSecrets, clock.UtcNow);
        if (binding.TenantId != connector.TenantId || binding.ConnectorId != connectorId
            || verified.LiveMode != binding.LiveMode || string.IsNullOrWhiteSpace(verified.EventId)
            || verified.EventId.Length > 200 || verified.EventType.Length > 100 || verified.PayloadHash.Length > 128)
            throw new InvalidStateException("Stripe notification does not match its connector.");
        await scope.ServiceProvider.GetRequiredService<IModuleGate>()
            .EnsureEnabledAsync(connector.TenantId, ModuleIds.Finance, cancellationToken);
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var existing = await context.PartnerWebhookEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.ConnectorId == connectorId && e.ProviderEventId == verified.EventId, cancellationToken);
        if (existing is not null) return; // The accepted row and retryable outbox committed together.

        Guid? intentId = null;
        Guid? orderId = null;
        if (verified.Supported)
        {
            if (verified.PaymentIntentId is not { } candidateIntent || verified.OrderId is not { } candidateOrder
                || verified.TenantId != connector.TenantId || verified.ConnectorId != connectorId)
                throw new InvalidStateException("Stripe notification is missing its payment correlation.");
            var intent = await context.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(p => p.Id == candidateIntent
                && p.TenantId == connector.TenantId, cancellationToken)
                ?? throw new InvalidOperationException("Stripe payment correlation is not yet available.");
            if (intent.OrderId != candidateOrder || intent.ConnectorId != connectorId || intent.ProviderCode != "Stripe"
                || intent.ProviderAccountId != binding.ProviderAccountId || intent.ProviderLiveMode != binding.LiveMode
                || (intent.ProviderReference is not null && verified.SessionId is not null && intent.ProviderReference != verified.SessionId)
                || (intent.ProviderPaymentIntentReference is not null && verified.ProviderPaymentIntentId is not null
                    && intent.ProviderPaymentIntentReference != verified.ProviderPaymentIntentId))
                throw new InvalidStateException("Stripe notification does not match its payment attempt.");
            intentId = intent.Id;
            orderId = intent.OrderId;
        }

        using var orderScope = logger.BeginOrderScope(orderId ?? Guid.Empty, paymentIntentId: intentId);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, orderId);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, connector.TenantId);
        logger.WebhookReceived(orderId ?? Guid.Empty, connector.TenantId, "Stripe", verified.EventType);
        var inbox = new PartnerWebhookEvent
        {
            TenantId = connector.TenantId,
            ConnectorId = connectorId,
            ProviderCode = "Stripe",
            Category = "Collection",
            EventType = verified.EventType,
            ProviderEventId = verified.EventId,
            ProviderReference = verified.SessionId ?? verified.ProviderPaymentIntentId ?? string.Empty,
            ClientReference = intentId?.ToString("N") ?? string.Empty,
            PayloadHash = verified.PayloadHash,
            RawPayload = JsonSerializer.Serialize(new { verified.EventId, verified.EventType, verified.LiveMode }),
            SignatureValid = true,
            ReceivedAt = clock.UtcNow,
            ProcessingStatus = verified.Supported ? "Received" : "Ignored",
            ProcessedAt = verified.Supported ? null : clock.UtcNow
        };
        context.PartnerWebhookEvents.Add(inbox);
        if (intentId is { } paymentIntentId)
            context.EnqueueIntegrationEvent(new CheckoutPaymentReconciliationRequestedEvent(connector.TenantId, paymentIntentId, inbox.Id));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Only a durable winning inbox proves this was a duplicate, not a lost acceptance.
            await using var checkScope = scopeFactory.CreateAsyncScope();
            checkScope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = connector.TenantId;
            var check = checkScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            if (!await check.PartnerWebhookEvents.AsNoTracking().AnyAsync(e => e.ConnectorId == connectorId
                    && e.ProviderEventId == verified.EventId && e.SignatureValid, cancellationToken)) throw;
        }
        activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
        logger.WebhookProcessed(orderId ?? Guid.Empty, connector.TenantId, "Stripe", verified.EventType, MoneyActionOutcomes.Success);
    }
}
