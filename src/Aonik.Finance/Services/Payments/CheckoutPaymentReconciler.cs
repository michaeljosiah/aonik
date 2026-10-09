using System.Diagnostics;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Events.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.Payments;

internal sealed class CheckoutPaymentReconciler(
    FinanceDbContext db,
    ITenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    IEnumerable<IPaymentProviderGateway> gateways,
    IClock clock,
    ILogger<CheckoutPaymentReconciler> logger) : ICheckoutPaymentReconciler
{
    public async Task<PaymentIntentStateRef> ReconcileAsync(Guid paymentIntentId, bool expire = false,
        Guid? webhookEventId = null, string? candidateSessionId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var intent = await db.PaymentIntents.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == paymentIntentId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Payment attempt was not found.");
        RequireStripe(intent);

        if (webhookEventId is { } eventId)
        {
            var inbox = await db.PartnerWebhookEvents.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == eventId && e.TenantId == tenantId
                    && e.ConnectorId == intent.ConnectorId && e.SignatureValid, cancellationToken)
                ?? throw new InvalidStateException("Verified payment notification was not found.");
            if (inbox.ClientReference != intent.Id.ToString("N"))
                throw new InvalidStateException("Payment notification does not match this attempt.");
            if (inbox.ProcessedAt is not null) return State(intent);
            if (inbox.ProviderReference.StartsWith("cs_", StringComparison.Ordinal))
                candidateSessionId = inbox.ProviderReference;
        }
        else if (intent.Status is nameof(PaymentStatus.Captured) or nameof(PaymentStatus.Cancelled))
        {
            return State(intent);
        }

        if (string.IsNullOrEmpty(intent.ProviderReference) && string.IsNullOrEmpty(candidateSessionId))
        {
            if (expire && intent.Status == nameof(PaymentStatus.Pending) && intent.ProviderRequestStartedAtUtc is null)
            {
                // Competes with initiation's native-version transition BEFORE its first external call.
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
                var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                var pending = await context.PaymentIntents.SingleAsync(p => p.Id == paymentIntentId && p.TenantId == tenantId, cancellationToken);
                if (pending.Status == nameof(PaymentStatus.Pending) && pending.ProviderRequestStartedAtUtc is null
                    && pending.ProviderReference is null)
                {
                    pending.Status = nameof(PaymentStatus.Cancelled);
                    await context.SaveChangesAsync(cancellationToken);
                    return State(pending);
                }
            }
            throw new InvalidStateException("The payment outcome is unknown; this attempt must remain locked.");
        }

        if (intent.ConnectorId is not { } connectorId || string.IsNullOrEmpty(intent.ProviderAccountId)
            || intent.ProviderLiveMode is not { } liveMode)
            throw new InvalidStateException("The payment attempt has no verified provider binding.");
        if (intent.ProviderReference is { } saved && candidateSessionId is { } candidate && saved != candidate)
            throw new InvalidStateException("The payment notification references another session.");
        var reference = new PaymentProviderCheckoutReference(connectorId, intent.ProviderAccountId, liveMode,
            intent.ProviderReference ?? candidateSessionId!, intent.ProviderPaymentIntentReference);
        var gateway = gateways.Single(g => string.Equals(g.ProviderCode, "Stripe", StringComparison.OrdinalIgnoreCase));
        // No database transaction spans the external read/expiration request.
        var snapshot = expire
            ? await gateway.ExpireCheckoutAsync(reference, cancellationToken)
            : await gateway.GetCheckoutAsync(reference, cancellationToken);
        return await ApplyAsync(paymentIntentId, snapshot, webhookEventId, cancellationToken);
    }

    public async Task<PaymentIntentStateRef> ApplyAsync(Guid paymentIntentId, PaymentProviderCheckoutSnapshot snapshot,
        Guid? webhookEventId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        using var activity = FinanceActivitySource.Source.StartActivity("payment.reconcile");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Webhook);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);
        activity?.SetTag(FinanceActivitySource.PaymentIntentIdTag, paymentIntentId);
        using var orderScope = logger.BeginOrderScope(snapshot.OrderId, paymentIntentId: paymentIntentId);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, snapshot.OrderId);
        logger.WebhookReceived(snapshot.OrderId, tenantId, "Stripe", "checkout.reconcile");
        try
        {
            // Each retry owns fresh tracked state: a rolled-back status/outbox is never reused.
            var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
                var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                await using var transaction = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(ct) : null;
                var intent = await context.PaymentIntents
                    .SingleOrDefaultAsync(p => p.Id == paymentIntentId && p.TenantId == tenantId, ct)
                    ?? throw new NotFoundException("Payment attempt was not found.");
                ValidateSnapshot(intent, snapshot);
                PartnerWebhookEvent? inbox = null;
                if (webhookEventId is { } eventId)
                {
                    inbox = await context.PartnerWebhookEvents.SingleOrDefaultAsync(e => e.Id == eventId
                        && e.TenantId == tenantId && e.ConnectorId == intent.ConnectorId && e.SignatureValid, ct)
                        ?? throw new InvalidStateException("Verified payment notification was not found.");
                    if (inbox.ClientReference != intent.Id.ToString("N"))
                        throw new InvalidStateException("Payment notification does not match this attempt.");
                }

                if (intent.Status != nameof(PaymentStatus.Captured))
                {
                    intent.ProviderReference = snapshot.SessionId;
                    intent.ProviderPaymentIntentReference = snapshot.ProviderPaymentIntentId ?? intent.ProviderPaymentIntentReference;
                    intent.NextActionRedirectUrl = snapshot.CheckoutUrl;
                    if (snapshot.Status == nameof(PaymentStatus.Captured))
                    {
                        if (intent.PayerPartyId is not { } payer || payer == Guid.Empty
                            || !await context.Parties.AnyAsync(p => p.Id == payer && p.TenantId == tenantId, ct)
                            || string.IsNullOrWhiteSpace(intent.PaymentMethodType))
                            throw new InvalidStateException("A captured payment requires its actual payer and payment method.");

                        // Internal poster saves remain inside this outer transaction.
                        await scope.ServiceProvider.GetRequiredService<LedgerPostingService>().PostPaymentCaptureAsync(intent, ct);
                        context.Payments.Add(new Payment
                        {
                            TenantId = tenantId,
                            PaymentIntentId = intent.Id,
                            ConnectorId = intent.ConnectorId,
                            Provider = "Stripe",
                            ProviderReference = snapshot.ProviderPaymentIntentId,
                            Amount = snapshot.ReceivedAmount,
                            Currency = intent.Currency,
                            CapturedAt = snapshot.CapturedAtUtc ?? clock.UtcNow,
                            OutcomeStatus = nameof(PaymentStatus.Captured),
                            OutcomeJson = JsonSerializer.Serialize(new { sessionId = snapshot.SessionId, accountId = snapshot.ProviderAccountId, liveMode = snapshot.LiveMode })
                        });
                        intent.Status = nameof(PaymentStatus.Captured);
                        context.EnqueueIntegrationEvent(new PaymentCompletedEvent(tenantId, intent.Id, intent.OrderId, intent.Amount, intent.Currency));
                    }
                    else if (intent.Status != nameof(PaymentStatus.Cancelled))
                    {
                        intent.Status = snapshot.CanNoLongerPay ? nameof(PaymentStatus.Cancelled) : snapshot.Status;
                    }
                }
                if (inbox is not null)
                {
                    inbox.ProcessingStatus = "Processed";
                    inbox.ProcessedAt = clock.UtcNow;
                    inbox.Error = null;
                }
                await context.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return State(intent);
            }, cancellationToken);
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
            logger.WebhookProcessed(snapshot.OrderId, tenantId, "Stripe", "checkout.reconcile", MoneyActionOutcomes.Success);
            return result;
        }
        catch (Exception exception)
        {
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
            activity?.SetStatus(ActivityStatusCode.Error, "Payment reconciliation failed.");
            // Provider payloads/secrets never become an error log field.
            logger.WebhookRejected(snapshot.OrderId, tenantId, "Stripe", "Payment reconciliation failed: " + exception.GetType().Name);
            throw;
        }
    }

    private static void RequireStripe(PaymentIntent intent)
    {
        if (!string.Equals(intent.ProviderCode, "Stripe", StringComparison.Ordinal))
            throw new InvalidStateException("This payment attempt is not a bound Stripe checkout.");
    }

    private static void ValidateSnapshot(PaymentIntent intent, PaymentProviderCheckoutSnapshot snapshot)
    {
        RequireStripe(intent);
        if (intent.TenantId != snapshot.TenantId || intent.Id != snapshot.PaymentIntentId || intent.OrderId != snapshot.OrderId
            || intent.ConnectorId != snapshot.ConnectorId || intent.ProviderAccountId != snapshot.ProviderAccountId
            || intent.ProviderLiveMode != snapshot.LiveMode || intent.Amount != snapshot.Amount
            || !string.Equals(intent.Currency, snapshot.Currency, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(snapshot.SessionId) || !snapshot.SessionId.StartsWith("cs_", StringComparison.Ordinal)
            || (intent.ProviderReference is not null && intent.ProviderReference != snapshot.SessionId)
            || (intent.ProviderPaymentIntentReference is not null && snapshot.ProviderPaymentIntentId is not null
                && intent.ProviderPaymentIntentReference != snapshot.ProviderPaymentIntentId))
            throw new InvalidStateException("The verified provider payment does not match the frozen local attempt.");
        if (!Enum.TryParse<PaymentStatus>(snapshot.Status, out var status) || !Enum.IsDefined(status))
            throw new InvalidStateException("The provider returned an unsupported payment state.");
        if (status == PaymentStatus.Captured && (snapshot.ReceivedAmount != intent.Amount
                || string.IsNullOrWhiteSpace(snapshot.ProviderPaymentIntentId) || snapshot.CanNoLongerPay))
            throw new InvalidStateException("The provider has not proven the exact captured amount.");
        if (snapshot.CanNoLongerPay && status is PaymentStatus.Authorized or PaymentStatus.Processing or PaymentStatus.RequiresAction)
            throw new InvalidStateException("The provider returned contradictory payment closure evidence.");
        if (status == PaymentStatus.Cancelled && !snapshot.CanNoLongerPay)
            throw new InvalidStateException("Cancellation requires confirmed unpaid provider closure.");
    }

    internal static PaymentIntentStateRef State(PaymentIntent intent) => new(intent.Id, intent.OrderId,
        intent.Amount, intent.Currency, intent.Status, intent.Status == nameof(PaymentStatus.Cancelled),
        intent.Status is nameof(PaymentStatus.Pending) or nameof(PaymentStatus.RequiresAction) or nameof(PaymentStatus.Failed)
            ? intent.NextActionRedirectUrl : null);
}
