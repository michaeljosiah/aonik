using System.Diagnostics;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Observability;
using Aonik.Finance.Services.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;

namespace Aonik.Finance.Services.Payments;

/// <summary>Persists one immutable attempt before contacting Stripe. Uncertain attempts stay locked.</summary>
internal sealed class CheckoutPaymentService(
    FinanceDbContext db, ITenantProvider tenantProvider, IStripeConnectorResolver connectors,
    IEnumerable<IPaymentProviderGateway> gateways, ICheckoutPaymentReconciler reconciler,
    IClock clock, ILogger<CheckoutPaymentService> logger, IServiceScopeFactory scopeFactory)
{
    public async Task<GuestPaymentIntentResponse> CreateAsync(
        CreateCommerceGuestPaymentIntentRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        using var activity = FinanceActivitySource.Source.StartActivity("checkout.payment.create");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Transmit);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, request.OrderId);
        using var orderScope = logger.BeginOrderScope(request.OrderId, paymentIntentId: request.PaymentIntentId);
        logger.OrderConfirmed(request.OrderId, tenantId, "Starting checkout payment attempt");
        try
        {
            ValidateRequest(request);
            var intent = await PrepareAsync(request, cancellationToken);
            var result = await ResumeAsync(intent, request, cancellationToken);
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
            logger.PaymentTransmitted(request.OrderId, tenantId, "Stripe", result.ProviderReference);
            return result;
        }
        catch (Exception exception)
        {
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
            activity?.SetStatus(ActivityStatusCode.Error, "Checkout initiation did not complete.");
            logger.PaymentTransmitFailed(request.OrderId, tenantId, "Stripe", exception.GetType().Name);
            throw;
        }
    }

    public async Task<PaymentIntentStateRef?> GetStateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var intent = await LoadAsync(id, cancellationToken);
        if (intent is null) return null;
        RequireStripe(intent);
        return State(intent);
    }

    public async Task<PaymentIntentStateRef> ExpireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var intent = await LoadAsync(id, cancellationToken) ?? throw new NotFoundException("Payment attempt was not found.");
        RequireStripe(intent);
        using var activity = FinanceActivitySource.Source.StartActivity("checkout.payment.expire");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Transmit);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, intent.TenantId);
        activity?.SetTag(FinanceActivitySource.OrderIdTag, intent.OrderId);
        activity?.SetTag(FinanceActivitySource.PaymentIntentIdTag, intent.Id);
        using var orderScope = logger.BeginOrderScope(intent.OrderId, paymentIntentId: intent.Id);
        logger.OrderConfirmed(intent.OrderId, intent.TenantId, "Reconciling checkout payment closure");
        try
        {
            PaymentIntentStateRef result;
            if (intent.Status is nameof(PaymentStatus.Captured) or nameof(PaymentStatus.Cancelled)) result = State(intent);
            else
            {
                if (intent.ProviderReference is null && intent.ProviderRequestStartedAtUtc is not null)
                {
                    // Recover a timed-out create with its original key and exact persisted parameters.
                    // Never guess that a missing response means the provider did not create a session.
                    await ResumeAsync(intent, null, cancellationToken);
                }
                result = await reconciler.ReconcileAsync(id, expire: true, cancellationToken: cancellationToken);
            }
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
            logger.PaymentTransmitted(intent.OrderId, intent.TenantId, "Stripe", intent.ProviderReference ?? string.Empty);
            return result;
        }
        catch (Exception exception)
        {
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
            activity?.SetStatus(ActivityStatusCode.Error, "Checkout closure could not be confirmed.");
            logger.PaymentTransmitFailed(intent.OrderId, intent.TenantId, "Stripe", exception.GetType().Name);
            throw;
        }
    }

    private async Task<PaymentIntent> PrepareAsync(CreateCommerceGuestPaymentIntentRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        // A fresh scope discards a rolled-back account claim, reservation and intent together.
        // Native account/intent contention retries locally; no provider call occurs in this loop.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
                    var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                    var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
                    await using var transaction = context.Database.IsRelational()
                        ? await context.Database.BeginTransactionAsync(ct) : null;
                    var order = await context.Orders.SingleOrDefaultAsync(o => o.Id == request.OrderId && o.TenantId == tenantId, ct)
                        ?? throw new NotFoundException("Order was not found.");
                    var intent = await context.PaymentIntents.SingleOrDefaultAsync(p => p.Id == request.PaymentIntentId && p.TenantId == tenantId, ct);
                    if (intent is not null)
                    {
                        ValidateAttempt(intent, request);
                        await loyalty.ValidateReplayAsync(intent, request.Loyalty, ct);
                        return intent;
                    }
                    if (order.OrderType != "ProductPurchase" || order.Status is not ("Draft" or "PendingFunding"))
                        throw new InvalidStateException("Only an unpaid product-purchase order can start checkout.");
                    if (order.PayerPartyId is not { } payer || payer == Guid.Empty
                        || !await context.Parties.AnyAsync(p => p.Id == payer && p.TenantId == tenantId, ct))
                        throw new InvalidStateException("Checkout requires its actual purchaser party.");
                    var expired = request.ProviderStartDeadlineUtc is { } deadline && clock.UtcNow >= deadline;
                    intent = new PaymentIntent
                    {
                        Id = request.PaymentIntentId!.Value, TenantId = tenantId, OrderId = order.Id, Amount = request.Amount,
                        Currency = "GBP", PayerPartyId = payer, PurposeType = "Order", PurposeId = order.Id,
                        PaymentMethodType = "Card", ProviderCode = "Stripe", IdempotencyKey = request.IdempotencyKey,
                        ProviderStartDeadlineUtc = request.ProviderStartDeadlineUtc,
                        Status = expired ? nameof(PaymentStatus.Cancelled) : nameof(PaymentStatus.Pending)
                    };
                    context.PaymentIntents.Add(intent);
                    // An expired attempt keeps its frozen instruction but needs no current policy.
                    var rejection = await loyalty.PrepareTrackedAsync(intent, request.Loyalty, ct);
                    if (rejection is not null)
                    {
                        intent.Status = nameof(PaymentStatus.Cancelled);
                        intent.FailureReason = rejection;
                    }
                    if (order.Status == "Draft") order.Status = "PendingFunding";
                    await context.SaveChangesAsync(ct);
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    return intent;
                }, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // The next fresh read either verifies the winning immutable attempt or retries
                // the competing account claim. A persistence failure is never cancellation proof.
            }
        }
    }

    private async Task<PaymentIntent> ClaimProviderStartAsync(PaymentIntent observed,
        PaymentProviderIntentRequest? request, DateTime startedAt, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
                    var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                    await using var transaction = context.Database.IsRelational()
                        ? await context.Database.BeginTransactionAsync(ct) : null;
                    var current = await context.PaymentIntents.SingleAsync(p => p.Id == observed.Id && p.TenantId == tenantId, ct);
                    if (current.ProviderRequestStartedAtUtc is not null || current.Status != nameof(PaymentStatus.Pending)) return current;
                    var expired = current.ProviderStartDeadlineUtc is { } deadline && clock.UtcNow >= deadline;
                    current.Status = expired ? nameof(PaymentStatus.Cancelled) : nameof(PaymentStatus.Processing);
                    if (expired)
                        await scope.ServiceProvider.GetRequiredService<LoyaltyService>().ReleaseTrackedAsync(current, ct);
                    else
                    {
                        if (request is null) throw new InvalidStateException("The provider request is unavailable.");
                        current.ConnectorId = request.ConnectorId;
                        current.ProviderAccountId = request.ProviderAccountId;
                        current.ProviderLiveMode = request.LiveMode;
                        current.ProviderCreateRequestJson = JsonSerializer.Serialize(request);
                        current.ProviderRequestStartedAtUtc = startedAt;
                    }
                    await context.SaveChangesAsync(ct);
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    return current;
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2) { }
        }
    }

    private async Task<GuestPaymentIntentResponse> ResumeAsync(PaymentIntent intent,
        CreateCommerceGuestPaymentIntentRequest? request, CancellationToken cancellationToken)
    {
        if (intent.ProviderReference is not null || intent.Status is nameof(PaymentStatus.Captured) or nameof(PaymentStatus.Cancelled))
            return Response(intent);

        if (intent.ProviderRequestStartedAtUtc is null)
        {
            if (request is null || intent.Status != nameof(PaymentStatus.Pending))
                throw new InvalidStateException("This payment attempt cannot start another provider request.");
            var deadlineReached = intent.ProviderStartDeadlineUtc is { } deadline && clock.UtcNow >= deadline;
            StripeConnectorBinding? binding = null;
            if (!deadlineReached)
            {
                binding = await connectors.ResolveSelectedAsync(cancellationToken);
                if (binding.TenantId != intent.TenantId)
                    throw new InvalidStateException("The payment connector belongs to another tenant.");
            }
            var startedAt = clock.UtcNow;
            deadlineReached |= intent.ProviderStartDeadlineUtc is { } currentDeadline && startedAt >= currentDeadline;
            var providerRequest = deadlineReached ? null : new PaymentProviderIntentRequest(intent.OrderId, intent.Amount, intent.Currency,
                "Card", ReturnUrl(request.ReturnUrl, binding!.ReturnOrigin), ReturnUrl(request.CancelUrl, binding.ReturnOrigin),
                $"ORD-{intent.OrderId:N}", intent.Id, binding.ConnectorId, binding.ProviderAccountId, binding.LiveMode, intent.IdempotencyKey);
            // A native row-version race with local cancellation must be won BEFORE any HTTP.
            // Missing state is not closure: even an expired first call persists this exact attempt,
            // then claims Cancelled so a delayed creator cannot subsequently start its provider request.
            intent = await ClaimProviderStartAsync(intent, providerRequest, startedAt, cancellationToken);
            if (intent.Status is nameof(PaymentStatus.Cancelled) or nameof(PaymentStatus.Captured)
                || intent.ProviderReference is not null) return Response(intent);
            if (intent.ProviderRequestStartedAtUtc is null)
                throw new InvalidStateException("The payment attempt cannot start a provider request.");
        }

        // Stripe may discard idempotency keys after 24 hours. A stale unknown create requires
        // operator reconciliation; issuing the same key again could otherwise create new money.
        if (clock.UtcNow - intent.ProviderRequestStartedAtUtc!.Value >= TimeSpan.FromHours(23))
            throw new InvalidStateException("This payment attempt requires provider reconciliation before retry.");
        var frozen = JsonSerializer.Deserialize<PaymentProviderIntentRequest>(intent.ProviderCreateRequestJson
            ?? throw new InvalidStateException("The original provider request is missing."))
            ?? throw new InvalidStateException("The original provider request is invalid.");
        if (frozen.PaymentIntentId != intent.Id || frozen.OrderId != intent.OrderId || frozen.Amount != intent.Amount
            || frozen.Currency != intent.Currency || frozen.IdempotencyKey != intent.IdempotencyKey
            || frozen.ConnectorId != intent.ConnectorId || frozen.ProviderAccountId != intent.ProviderAccountId
            || frozen.LiveMode != intent.ProviderLiveMode)
            throw new InvalidStateException("The original provider request does not match this attempt.");
        var gateway = gateways.Single(g => string.Equals(g.ProviderCode, "Stripe", StringComparison.OrdinalIgnoreCase));
        var providerResult = await gateway.CreateIntentAsync(frozen, cancellationToken);
        var snapshot = providerResult.Checkout ?? throw new InvalidStateException("The provider did not return verified checkout state.");
        await reconciler.ApplyAsync(intent.Id, snapshot, cancellationToken: cancellationToken);
        return Response(await LoadAsync(intent.Id, cancellationToken)
            ?? throw new InvalidStateException("The recorded payment attempt is unavailable."));
    }

    private Task<PaymentIntent?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        return db.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);
    }

    private static void ValidateRequest(CreateCommerceGuestPaymentIntentRequest request)
    {
        if (request.ProviderStartDeadlineUtc is { Kind: not DateTimeKind.Utc })
            throw new InvalidStateException("The provider start deadline must be UTC.");
        if (request.PaymentIntentId is null || request.PaymentIntentId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200
            || request.IdempotencyKey.Any(char.IsControl))
            throw new InvalidStateException("Checkout requires a stable server payment attempt and idempotency key.");
        if (!string.Equals(request.Provider, "Stripe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.PaymentMethodType, "Card", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.Currency, "GBP", StringComparison.OrdinalIgnoreCase)
            || request.Amount < 0.30m || request.Amount > 999999.99m || decimal.Round(request.Amount, 2) != request.Amount)
            throw new InvalidStateException("Checkout requires a GBP card amount of at least 30p in whole pennies.");
    }

    private static void ValidateAttempt(PaymentIntent intent, CreateCommerceGuestPaymentIntentRequest request)
    {
        RequireStripe(intent);
        if (intent.OrderId != request.OrderId || intent.Amount != request.Amount
            || !string.Equals(intent.Currency, request.Currency, StringComparison.OrdinalIgnoreCase)
            || intent.IdempotencyKey != request.IdempotencyKey || intent.PaymentMethodType != "Card"
            || intent.ProviderStartDeadlineUtc != request.ProviderStartDeadlineUtc)
            throw new InvalidStateException("This payment attempt is bound to different checkout details.");
    }

    private static string ReturnUrl(string? supplied, string origin)
    {
        var value = supplied ?? origin.TrimEnd('/') + "/checkout";
        if (value.Length > 2000 || value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.Equals(uri.GetLeftPart(UriPartial.Authority), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidStateException("Checkout return URLs must use the configured HTTPS storefront origin.");
        return uri.AbsoluteUri;
    }

    private static void RequireStripe(PaymentIntent intent)
    {
        if (intent.ProviderCode != "Stripe") throw new InvalidStateException("This is not a Stripe checkout attempt.");
    }

    private static string? CheckoutUrl(PaymentIntent intent) =>
        intent.Status is nameof(PaymentStatus.Pending) or nameof(PaymentStatus.RequiresAction) or nameof(PaymentStatus.Failed)
            ? intent.NextActionRedirectUrl : null;

    private static PaymentIntentStateRef State(PaymentIntent intent) => new(intent.Id, intent.OrderId, intent.Amount,
        intent.Currency, intent.Status, intent.Status == nameof(PaymentStatus.Cancelled), CheckoutUrl(intent));

    private static GuestPaymentIntentResponse Response(PaymentIntent intent) => new(intent.Id, intent.OrderId, intent.Amount,
        intent.Currency, intent.Status, "Stripe", intent.ProviderReference ?? string.Empty, null, CheckoutUrl(intent), intent.CreatedAt);
}
