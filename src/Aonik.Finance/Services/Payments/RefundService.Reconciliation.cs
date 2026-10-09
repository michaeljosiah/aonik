using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Payments;

internal sealed partial class RefundService
{
    Task IRefundReconciler.ReconcileAsync(Guid refundId, Guid? webhookEventId, CancellationToken cancellationToken)
        => ReconcileCoreAsync(refundId, webhookEventId, cancellationToken);

    private async Task ReconcileCoreAsync(Guid refundId, Guid? webhookEventId, CancellationToken ct)
    {
        var row = await db.Refunds.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == refundId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Refund was not found.");
        var snapshot = RefundSnapshot.Read(row);
        string? candidate = null;
        if (webhookEventId is { } eventId)
        {
            var inbox = await db.PartnerWebhookEvents.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
                && x.Id == eventId && x.ConnectorId == row.ConnectorId && x.Category == "Refund" && x.SignatureValid, ct)
                ?? throw new InvalidStateException("The verified refund notification is unavailable.");
            if (inbox.ClientReference != row.Id.ToString("N")) throw new InvalidStateException("The refund notification belongs to another request.");
            if (inbox.ProcessedAt is not null) return;
            candidate = inbox.ProviderReference;
        }
        else if (row.Status == "Failed") return;
        if (snapshot.Provider is null)
        {
            if (webhookEventId is not null || snapshot.CashAmount != 0)
                throw new InvalidStateException("An internal refund cannot accept provider evidence.");
            await ApplyAsync(refundId, null, null, ct);
            return;
        }
        var provider = snapshot.Provider;
        var gateway = gateways.Single(x => x.ProviderCode == "Stripe");
        try
        {
            PaymentProviderRefundSnapshot? observed;
            var providerId = row.ProviderReference ?? candidate;
            if (providerId is not null)
                observed = await gateway.GetRefundAsync(provider, providerId, ct);
            else
            {
                if (row.ProviderRequestStartedAtUtc is null)
                {
                    var budget = await gateway.GetRefundBudgetAsync(provider, ct);
                    if (!await BudgetMatchesAsync(snapshot, budget, ct))
                    {
                        await RecordUncertainAsync(refundId, "NeedsReconciliation", "An external refund or changed provider balance requires reconciliation.", ct);
                        return;
                    }
                    row = await InTransactionAsync(async (service, token) =>
                    {
                        var current = await service.Db.Refunds.SingleAsync(x => x.TenantId == service.TenantId && x.Id == refundId, token);
                        var intent = await service.IntentAsync(snapshot.Source, token);
                        if (current.Status == "Failed" || current.EffectsAppliedAtUtc is not null) return current;
                        current.ProviderRequestStartedAtUtc ??= service.Clock.UtcNow;
                        current.Status = "Unknown";
                        service.Touch(intent);
                        return current;
                    }, ct);
                }
                // An aged provider key cannot authorize a new POST: lookup absence remains unknown.
                if (row.ProviderRequestStartedAtUtc is null || row.Status == "Failed") return;
                observed = clock.UtcNow - row.ProviderRequestStartedAtUtc.Value < TimeSpan.FromHours(23)
                    ? await gateway.CreateRefundAsync(provider, ct)
                    : await gateway.FindRefundAsync(provider, ct);
                if (observed is null)
                {
                    await RecordUncertainAsync(refundId, "NeedsReconciliation", "The original refund outcome could not be proved. Do not issue another refund.", ct);
                    return;
                }
            }
            await ApplyAsync(refundId, observed, webhookEventId, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A provider or local commit failure cannot release budgets or authorize a second request.
            await RecordUncertainAsync(refundId, "Unknown", "The refund outcome must be reconciled using this request.", ct);
            throw new InvalidOperationException("Refund reconciliation is incomplete; the original request remains reserved.");
        }
    }

    private async Task<bool> BudgetMatchesAsync(RefundSnapshot snapshot, PaymentProviderRefundBudget budget, CancellationToken ct)
    {
        if (budget.Currency != snapshot.Source.Currency || budget.CapturedAmount != snapshot.Source.CardAmount
            || budget.Refunds.Select(x => x.ProviderRefundId).Distinct().Count() != budget.Refunds.Count) return false;
        var rows = await RowsAsync(snapshot.Source.OrderId, ct);
        foreach (var observed in budget.Refunds)
        {
            var known = rows.SingleOrDefault(x => x.Id == observed.LocalRefundId && x.ProviderReference == observed.ProviderRefundId);
            if (known?.RequestSnapshotJson is null || known.PaymentIntentId != snapshot.Source.PaymentIntentId
                || observed.Currency != snapshot.Source.Currency || RefundSnapshot.Read(known).CashAmount != observed.Amount
                || (known.Status == "Succeeded" ? observed.Status != "succeeded"
                    : known.Status == "Failed" ? observed.Status is not ("failed" or "canceled") : true)) return false;
        }
        return rows.Where(x => x.Status == "Succeeded" && x.PaymentIntentId == snapshot.Source.PaymentIntentId
                && x.RequestSnapshotJson is not null && RefundSnapshot.Read(x).CashAmount > 0)
            .All(x => budget.Refunds.Any(r => r.ProviderRefundId == x.ProviderReference));
    }

    private Task<bool> RecordUncertainAsync(Guid refundId, string status, string reason, CancellationToken ct) =>
        InTransactionAsync(async (service, token) =>
        {
            var row = await service.Db.Refunds.SingleAsync(x => x.TenantId == service.TenantId && x.Id == refundId, token);
            var snapshot = RefundSnapshot.Read(row);
            var intent = await service.IntentAsync(snapshot.Source, token);
            if (row.EffectsAppliedAtUtc is null && row.Status != "Failed")
            {
                row.Status = status;
                row.FailureReason = reason;
                service.Touch(intent);
            }
            return true;
        }, ct);

    internal Task<bool> ApplyAsync(Guid refundId, PaymentProviderRefundSnapshot? observed, Guid? webhookEventId,
        CancellationToken ct = default) => InTransactionAsync(async (service, token) =>
    {
        var row = await service.Db.Refunds.SingleAsync(x => x.TenantId == service.TenantId && x.Id == refundId && !x.IsDeleted, token);
        var snapshot = RefundSnapshot.Read(row);
        var intent = await service.IntentAsync(snapshot.Source, token);
        if (snapshot.Provider is { } expected)
        {
            if (observed is null || observed.TenantId != service.TenantId || observed.RefundId != row.Id
                || observed.PaymentIntentId != intent.Id || observed.OrderId != intent.OrderId
                || observed.ConnectorId != expected.ConnectorId || observed.ProviderAccountId != expected.ProviderAccountId
                || observed.LiveMode != expected.LiveMode || observed.ProviderPaymentIntentId != expected.ProviderPaymentIntentId
                || observed.Amount != snapshot.CashAmount || observed.Currency != row.Currency
                || !observed.ProviderRefundId.StartsWith("re_", StringComparison.Ordinal)
                || (row.ProviderReference is not null && row.ProviderReference != observed.ProviderRefundId))
                throw new InvalidStateException("Provider refund evidence does not match the authorized request.");
            row.ProviderReference = observed.ProviderRefundId;
            row.RawResponseJson = JsonSerializer.Serialize(new { observed.Status, observed.BalanceTransactionId,
                observed.FailureBalanceTransactionId, observed.FailureCode }, RefundSnapshot.Json);
        }
        else if (observed is not null || snapshot.CashAmount != 0)
            throw new InvalidStateException("An internal refund cannot contain external cash evidence.");
        var status = observed?.Status ?? "succeeded";
        if (status == "succeeded")
        {
            // A late bank-return correction requires explicit resolution, never automatic reapplication.
            if (row.EffectsAppliedAtUtc is null)
            {
                if (row.Status == "Failed") throw new InvalidStateException("A previously failed refund needs accounting reconciliation.");
                row.Status = "Unknown";
                var originalCash = await service.RequireFundingAsync(intent, snapshot.Source, token);
                if (originalCash != snapshot.CashLedger)
                    throw new InvalidStateException("The original cash funding evidence changed.");
                await service.PostCashAsync(row, snapshot, false, token);
                await service.Gifts.CompleteRefundTrackedAsync(row, snapshot.Gift, token);
                if (snapshot.Loyalty is not null) await service.Loyalty.ReverseRefundTrackedAsync(snapshot.Loyalty, token);
                row.EffectsAppliedAtUtc = service.Clock.UtcNow;
                row.Status = "Succeeded";
                row.FailureReason = null;
                service.History(row, snapshot, "RefundCompleted");
            }
        }
        else if (status is "failed" or "canceled")
        {
            if (row.EffectsAppliedAtUtc is not null)
            {
                if (string.IsNullOrWhiteSpace(observed?.FailureBalanceTransactionId))
                    row.FailureReason = "The provider reports a late failure; returned cash evidence needs reconciliation.";
                else
                {
                    await service.PostCashAsync(row, snapshot, true, token);
                    row.FailureReason = "Cash returned after the refund. Gift and points obligations remain recorded; accounting resolution is required.";
                }
                var changed = row.Status != "NeedsReconciliation";
                row.Status = "NeedsReconciliation";
                if (changed) service.History(row, snapshot, "RefundRequiresReconciliation");
            }
            else
            {
                await service.Gifts.ReleaseRefundTrackedAsync(row, snapshot.Gift, token);
                var changed = row.Status != "Failed";
                row.Status = "Failed";
                row.FailureReason = "The provider confirmed that this refund did not complete.";
                if (changed) service.History(row, snapshot, "RefundFailed");
            }
        }
        else if (row.EffectsAppliedAtUtc is null && row.Status != "Failed")
        {
            row.Status = status is "pending" or "requires_action" ? "Pending" : "Unknown";
            row.FailureReason = "The original refund is awaiting a confirmed provider outcome.";
        }
        service.Touch(intent);
        if (webhookEventId is { } eventId)
        {
            var inbox = await service.Db.PartnerWebhookEvents.SingleAsync(x => x.TenantId == service.TenantId
                && x.Id == eventId && x.Category == "Refund" && x.SignatureValid, token);
            if (inbox.ClientReference != row.Id.ToString("N") || inbox.ConnectorId != row.ConnectorId
                || inbox.ProviderReference != row.ProviderReference) throw new InvalidStateException("Refund notification correlation changed.");
            inbox.ProcessedAt = service.Clock.UtcNow;
            inbox.ProcessingStatus = row.Status == "NeedsReconciliation" ? "NeedsReconciliation" : "Processed";
        }
        return true;
    }, ct);
}
