using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.GiftCards;
using Aonik.Finance.Services.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Events.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Services.Payments;

internal sealed partial class RefundService(FinanceDbContext db, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IPermissionService permissions, IClock clock,
    IServiceScopeFactory scopes, ICheckoutRefundSourceReader sources, GiftCardService gifts,
    LoyaltyService loyalty, IJournalWriter journals, IEnumerable<IPaymentProviderGateway> gateways)
    : FinanceServiceBase(currentUser, permissions), IOrderRefundService, IRefundReconciler
{
    private Guid TenantId => tenantProvider.GetCurrentTenantId();
    private FinanceDbContext Db => db;
    private GiftCardService Gifts => gifts;
    private LoyaltyService Loyalty => loyalty;
    private IClock Clock => clock;

    public async Task<RefundContextDto> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Read", cancellationToken);
        await RequireOrderAsync(orderId, cancellationToken);
        var rows = await RowsAsync(orderId, cancellationToken);
        var history = rows.Where(x => x.RequestSnapshotJson is not null).OrderByDescending(x => x.CreatedAt).Select(Map).ToArray();
        try
        {
            var source = await SourceAsync(orderId, cancellationToken);
            var prior = Prior(rows);
            var components = RefundCalculation.Components(source, prior);
            var remaining = components.Sum(x => x.RemainingAmount);
            var status = rows.Any(x => x.Status == "NeedsReconciliation") ? "NeedsReconciliation"
                : rows.Any(IsUnresolved) ? "RefundPending"
                : prior.Count == 0 ? "None" : components.All(x => !x.CanRefund) ? "Refunded" : "PartiallyRefunded";
            var reason = await DisabledReasonAsync(orderId, rows, cancellationToken);
            return new(source.Currency, reason is null && components.Any(x => x.CanRefund), reason, remaining, status, components, history);
        }
        catch (InvalidStateException exception)
        {
            return new("GBP", false, exception.Message, 0m, rows.Any(IsUnresolved) ? "NeedsReconciliation" : "Unavailable", [], history);
        }
    }

    public async Task<RefundDto?> GetRefundAsync(Guid orderId, Guid refundId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Read", cancellationToken);
        await RequireOrderAsync(orderId, cancellationToken);
        var row = await db.Refunds.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == refundId && !x.IsDeleted, cancellationToken);
        return row?.RequestSnapshotJson is not null && RefundSnapshot.Read(row).Source.OrderId == orderId ? Map(row) : null;
    }

    public async Task<RefundPreviewDto> PreviewAsync(Guid orderId, RefundDraft draft, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Refund", cancellationToken);
        var source = await SourceAsync(orderId, cancellationToken);
        var rows = await RowsAsync(orderId, cancellationToken);
        var reason = await DisabledReasonAsync(orderId, rows, cancellationToken);
        if (reason is not null) throw new InvalidStateException(reason);
        var result = RefundCalculation.Calculate(source, draft, Prior(rows));
        var intent = await IntentAsync(source, cancellationToken);
        await RequireFundingAsync(intent, source, cancellationToken);
        await gifts.ReadRefundInstructionAsync(intent, result.Preview.GiftAmount, Reclaim(source, result.Components), cancellationToken, ReclaimOrderItem(source, result.Components));
        return result.Preview;
    }

    public async Task<RefundDto> RequestAsync(Guid orderId, RefundRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Refund", cancellationToken);
        if (request is null || request.RefundId == Guid.Empty || string.IsNullOrWhiteSpace(request.ExpectedPreviewVersion)
            || request.ExpectedPreviewVersion.Length != 64) throw new InvalidStateException("A refund identity and current preview are required.");
        var draft = RefundCalculation.Normalize(new(request.Reason, request.Selections));
        request = request with { Reason = draft.Reason, Selections = draft.Selections };
        var actor = CurrentUserProvider.GetCurrentUserId();
        return await InTransactionAsync(async (service, ct) =>
        {
            var existing = await service.Db.Refunds.SingleOrDefaultAsync(x => x.TenantId == service.TenantId
                && x.Id == request.RefundId && !x.IsDeleted, ct);
            if (existing is not null)
            {
                var saved = RefundSnapshot.Read(existing);
                if (saved.Source.OrderId != orderId || RefundSnapshot.Fingerprint(saved.Request) != RefundSnapshot.Fingerprint(request))
                    throw new InvalidStateException("This refund identity already has different request details.");
                return Map(existing);
            }
            var source = await service.SourceAsync(orderId, ct);
            // Every admission and outcome writer claims this native version BEFORE reading mutable refund budgets.
            var intent = await service.IntentAsync(source, ct);
            var rows = await service.RowsAsync(orderId, ct);
            var reason = await service.DisabledReasonAsync(orderId, rows, ct);
            if (reason is not null) throw new InvalidStateException(reason);
            var calculation = RefundCalculation.Calculate(source, draft, Prior(rows));
            if (calculation.Preview.Version != request.ExpectedPreviewVersion)
                throw new InvalidStateException("The refund breakdown changed. Review a fresh preview.");
            var cashLedger = await service.RequireFundingAsync(intent, source, ct);
            var gift = await service.Gifts.ReadRefundInstructionAsync(intent, calculation.Preview.GiftAmount,
                Reclaim(source, calculation.Components), ct, ReclaimOrderItem(source, calculation.Components));
            var pointLines = calculation.Components.Where(x => x.EarnedPointsToReverse > 0 || x.RedeemedPointsToRestore > 0)
                .Select(x => new LoyaltyRefundLine(x.OrderItemId ?? throw new InvalidStateException("Points require an original order line."),
                    x.EarnedPointsToReverse, x.RedeemedPointsToRestore)).ToArray();
            PaymentProviderRefundRequest? provider = calculation.Preview.CashAmount == 0 ? null : new(request.RefundId,
                intent.Id, orderId, intent.ConnectorId!.Value, intent.ProviderAccountId!, intent.ProviderLiveMode!.Value,
                intent.ProviderPaymentIntentReference!, calculation.Preview.CashAmount, source.Currency, $"refund:{request.RefundId:N}");
            var snapshot = new RefundSnapshot(source, request, actor, calculation.Components, calculation.Preview.CashAmount,
                calculation.Preview.GiftAmount, cashLedger, provider, gift,
                pointLines.Length == 0 ? null : new(request.RefundId, orderId, intent.Id, pointLines));
            var refund = new Refund { Id = request.RefundId, TenantId = service.TenantId, PaymentIntentId = intent.Id,
                PaymentId = cashLedger?.ReceiptId, ConnectorId = provider?.ConnectorId, ClientReference = $"refund:{request.RefundId:N}",
                Amount = calculation.Preview.Total, Currency = source.Currency, Reason = request.Reason, Status = "Requested",
                RequestSnapshotJson = snapshot.Serialize(), CreatedAt = service.Clock.UtcNow, CreatedBy = actor };
            await service.Gifts.PrepareRefundTrackedAsync(refund, gift, ct);
            service.Db.Refunds.Add(refund);
            service.Touch(intent);
            service.History(refund, snapshot, "RefundRequested");
            service.Db.EnqueueIntegrationEvent(new RefundReconciliationRequestedEvent(service.TenantId, refund.Id));
            return Map(refund);
        }, cancellationToken);
    }

    public async Task<RefundDto> ReconcileAsync(Guid orderId, Guid refundId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Payment.Refund", cancellationToken);
        return await InTransactionAsync(async (service, ct) =>
        {
            var row = await service.Db.Refunds.SingleOrDefaultAsync(x => x.TenantId == service.TenantId && x.Id == refundId && !x.IsDeleted, ct)
                ?? throw new NotFoundException("Refund was not found.");
            var snapshot = RefundSnapshot.Read(row);
            if (snapshot.Source.OrderId != orderId) throw new NotFoundException("Refund was not found.");
            if (row.Status != "Failed") service.Db.EnqueueIntegrationEvent(new RefundReconciliationRequestedEvent(service.TenantId, row.Id));
            return Map(row);
        }, cancellationToken);
    }

    private async Task<CheckoutRefundSource> SourceAsync(Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(orderId, ct);
        return await sources.ReadAsync(orderId, ct) ?? throw new InvalidStateException("This order has no supported checkout refund breakdown.");
    }
    private async Task RequireOrderAsync(Guid orderId, CancellationToken ct)
    {
        if (!await db.Orders.AnyAsync(x => x.TenantId == TenantId && x.Id == orderId && !x.IsDeleted, ct))
            throw new NotFoundException("Order was not found.");
    }
    private async Task<PaymentIntent> IntentAsync(CheckoutRefundSource source, CancellationToken ct)
    {
        var intent = await db.PaymentIntents.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == source.PaymentIntentId && !x.IsDeleted, ct)
            ?? throw new InvalidStateException("The original captured payment is unavailable.");
        if (intent.OrderId != source.OrderId || intent.Status != "Captured" || intent.Amount != source.Total
            || intent.Currency != source.Currency || intent.ProviderCode is not ("Stripe" or "GiftCard"))
            throw new InvalidStateException("Only the original supported captured checkout can be refunded.");
        return intent;
    }
    private Task<List<Refund>> RowsAsync(Guid orderId, CancellationToken ct) => db.Refunds.AsNoTracking()
        .Where(x => x.TenantId == TenantId && !x.IsDeleted && db.PaymentIntents.Any(p => p.TenantId == TenantId
            && p.OrderId == orderId && p.Id == x.PaymentIntentId)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    private static bool IsUnresolved(Refund row) => row.Status is not ("Succeeded" or "Failed");
    private static IReadOnlyList<RefundSnapshot> Prior(IReadOnlyList<Refund> rows)
    {
        if (rows.Any(x => x.RequestSnapshotJson is null || (x.Status == "Succeeded" && x.EffectsAppliedAtUtc is null)
            || (x.Status == "Failed" && x.EffectsAppliedAtUtc is not null)))
            throw new InvalidStateException("A previous refund requires accounting reconciliation.");
        return rows.Where(x => x.EffectsAppliedAtUtc is not null).Select(RefundSnapshot.Read).ToArray();
    }
    private async Task<string?> DisabledReasonAsync(Guid orderId, IReadOnlyList<Refund> rows, CancellationToken ct)
    {
        if (rows.Count >= 1000) return "This order's refund history requires accounting support.";
        if (rows.Any(IsUnresolved)) return "Resolve the existing refund before requesting another.";
        var intentIds = (await db.PaymentIntents.Where(x => x.TenantId == TenantId && x.OrderId == orderId)
            .Select(x => x.Id).ToListAsync(ct)).Select(x => x.ToString("N")).ToArray();
        if (await db.PartnerWebhookEvents.AnyAsync(x => x.TenantId == TenantId && x.Category == "Refund"
            && x.ProcessingStatus == "NeedsReconciliation" && intentIds.Contains(x.ClientReference), ct))
            return "An external refund requires accounting reconciliation.";
        return null;
    }
    // A refund has one reclaim hold. Staff select the original card's order component;
    // multiple cards can be reclaimed using separate, sequential refund requests.
    private static Guid? ReclaimOrderItem(CheckoutRefundSource source, IReadOnlyList<RefundAllocation> components)
    {
        var cards = components.Where(x => source.Components.Single(c => c.ComponentId == x.ComponentId).Kind == "GiftCardValue").ToArray();
        if (cards.Length > 1) throw new InvalidStateException("Refund one gift-card value at a time; each card needs its own reclaim hold.");
        return cards.SingleOrDefault()?.OrderItemId;
    }
    private static decimal Reclaim(CheckoutRefundSource source, IReadOnlyList<RefundAllocation> components) => components
        .Where(x => source.Components.Single(c => c.ComponentId == x.ComponentId).Kind == "GiftCardValue").Sum(x => x.Amount);
    private void Touch(PaymentIntent intent)
    {
        intent.UpdatedAt = clock.UtcNow;
        db.Entry(intent).Property(x => x.UpdatedAt).IsModified = true;
    }
    private void History(Refund refund, RefundSnapshot snapshot, string eventType)
    {
        db.OrderHistoryEvents.Add(new OrderHistoryEvent { TenantId = TenantId, OrderId = snapshot.Source.OrderId,
            EventType = eventType, EventAt = clock.UtcNow, ActorType = "User", ActorId = snapshot.ActorId ?? Guid.Empty,
            DetailsJson = JsonSerializer.Serialize(new { refundId = refund.Id, refund.Status, refund.Amount, refund.Currency,
                snapshot.CashAmount, snapshot.GiftAmount }, RefundSnapshot.Json) });
    }
    private static RefundDto Map(Refund row)
    {
        var snapshot = RefundSnapshot.Read(row);
        return new(row.Id, snapshot.Source.OrderId, row.Status, row.CreatedAt, snapshot.ActorId, row.Reason ?? "",
            row.Amount, row.Currency, snapshot.CashAmount, snapshot.GiftAmount,
            snapshot.Components.Sum(x => x.RedeemedPointsToRestore), snapshot.Components.Sum(x => x.EarnedPointsToReverse),
            row.EffectsAppliedAtUtc, row.FailureReason, row.Status != "Failed");
    }
    private async Task<T> InTransactionAsync<T>(Func<RefundService, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
                {
                    await using var scope = scopes.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = TenantId;
                    var service = scope.ServiceProvider.GetRequiredService<RefundService>();
                    await using var transaction = service.Db.Database.IsRelational() ? await service.Db.Database.BeginTransactionAsync(token) : null;
                    var result = await action(service, token);
                    await service.Db.SaveChangesAsync(token);
                    if (transaction is not null) await transaction.CommitAsync(token);
                    return result;
                }, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2) { }
            catch (DbUpdateException exception) when (attempt < 2
                && exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { }
        }
    }
}
