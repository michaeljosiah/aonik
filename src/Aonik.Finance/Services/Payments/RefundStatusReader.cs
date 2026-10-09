using System.Text.Json;

using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Payments;

internal sealed class RefundStatusReader(FinanceDbContext db, ITenantProvider tenantProvider) : IOrderRefundStatusReader
{
    public async Task<OrderRefundStatusDto> ReadAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var rows = await (from refund in db.Refunds.AsNoTracking()
            join intent in db.PaymentIntents.AsNoTracking() on refund.PaymentIntentId equals (Guid?)intent.Id
            join order in db.Orders.AsNoTracking() on intent.OrderId equals order.Id
            where refund.TenantId == tenantId && intent.TenantId == tenantId && order.TenantId == tenantId
                && !refund.IsDeleted && !intent.IsDeleted && !order.IsDeleted && order.Id == orderId
            orderby refund.CreatedAt, refund.Id
            select new { Refund = refund, IntentId = intent.Id, intent.Amount, intent.Currency, intent.Status })
            .Take(1001).ToListAsync(cancellationToken);
        if (rows.Count == 0) return new("None", 0m, 0m, 0m);
        if (rows.Count > 1000) return new("NeedsReconciliation", 0m, 0m, 0m);

        try
        {
            var snapshots = rows.Select(row => RefundSnapshot.Read(row.Refund)).ToArray();
            if (snapshots.Any(snapshot => snapshot.Source?.Components is null || snapshot.Components is null
                || snapshot.Source.Components.Any(component => component is null)
                || snapshot.Components.Any(component => component is null)))
                return new("NeedsReconciliation", 0m, 0m, 0m);
            var source = snapshots[0].Source;
            var binding = RefundSnapshot.Fingerprint(source);
            if (rows.Where((row, index) => row.Status != "Captured" || row.Amount != source.Total
                    || row.Currency != source.Currency || snapshots[index].Source.OrderId != orderId
                    || snapshots[index].Source.PaymentIntentId != row.IntentId
                    || RefundSnapshot.Fingerprint(snapshots[index].Source) != binding).Any())
                return new("NeedsReconciliation", 0m, 0m, 0m);

            // A late provider failure can correct a previously applied cash journal. Do not
            // continue presenting that row's original return amounts as verified returns.
            var completed = snapshots.Where((_, index) => rows[index].Refund.Status == "Succeeded"
                && rows[index].Refund.EffectsAppliedAtUtc is not null).ToArray();
            var cash = completed.Sum(snapshot => snapshot.CashAmount);
            var gift = completed.Sum(snapshot => snapshot.GiftAmount);
            var available = RefundCalculation.Components(source, completed);
            if (cash < 0 || gift < 0 || cash > source.CardAmount || gift > source.GiftAmount
                || completed.Any(snapshot => snapshot.CashAmount + snapshot.GiftAmount != snapshot.Components.Sum(line => line.Amount)))
                return new("NeedsReconciliation", 0m, 0m, 0m);

            var needsReconciliation = rows.Any(row => row.Refund.Status == "NeedsReconciliation"
                || (row.Refund.Status == "Succeeded" && row.Refund.EffectsAppliedAtUtc is null)
                || (row.Refund.Status != "Succeeded" && row.Refund.EffectsAppliedAtUtc is not null));
            var pending = rows.Any(row => row.Refund.Status is not ("Succeeded" or "Failed"));
            var status = needsReconciliation ? "NeedsReconciliation" : pending ? "RefundPending"
                : completed.Length == 0 ? "None"
                : cash == source.CardAmount && gift == source.GiftAmount && available.All(component => !component.CanRefund)
                    ? "Refunded" : "PartiallyRefunded";
            return new(status, cash, gift, cash + gift);
        }
        catch (Exception exception) when (exception is InvalidStateException or JsonException or OverflowException)
        {
            return new("NeedsReconciliation", 0m, 0m, 0m);
        }
    }
}
