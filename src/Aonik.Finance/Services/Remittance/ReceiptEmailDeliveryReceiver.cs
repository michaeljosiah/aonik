using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.Remittance;

internal sealed class ReceiptEmailDeliveryReceiver(FinanceDbContext db, ITenantProvider tenants, IClock clock) : IReceiptEmailDeliveryReceiver
{
    public async Task RecordAsync(ReceiptEmailDeliveryReport report, CancellationToken cancellationToken = default)
    {
        if (report.MessageId == Guid.Empty || string.IsNullOrWhiteSpace(report.EventId) || report.EventId.Length > 128 ||
            string.IsNullOrWhiteSpace(report.Recipient) || report.Recipient.Length > 320 || !ReceiptEmailDelivery.ReportStatuses.Contains(report.Status) ||
            report.AttemptedAt == default || report.AttemptedAt.UtcDateTime > clock.UtcNow.AddMinutes(5))
            throw new ArgumentException("Invalid receipt delivery report.");

        var tenantId = tenants.GetCurrentTenantId();
        var request = await db.OrderHistoryEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == report.MessageId &&
            item.TenantId == tenantId && item.EventType == "RemittanceReceiptEmailRequested", cancellationToken);
        if (request is null) return; // Other application emails sharing the ACS resource are expected.
        var details = ReceiptEmailDelivery.RequestDetails(request);
        if (!details.DeliveryTrackingEnabled || details.EmailHash != ReceiptEmailDelivery.Hash(report.Recipient.Trim().ToLowerInvariant()) ||
            report.AttemptedAt.UtcDateTime < DateTime.SpecifyKind(request.EventAt, DateTimeKind.Utc).AddMinutes(-5)) return;

        var eventId = ReceiptEmailDelivery.EventId(tenantId, request.Id, report.EventId);
        if (await db.OrderHistoryEvents.AsNoTracking().AnyAsync(item => item.Id == eventId && item.TenantId == tenantId, cancellationToken)) return;
        var entry = new OrderHistoryEvent
        {
            Id = eventId, TenantId = tenantId, OrderId = request.OrderId, EventType = ReceiptEmailDelivery.EventType(request.Id),
            ActorType = "System", ActorId = Guid.Empty, EventAt = report.AttemptedAt.UtcDateTime,
            DetailsJson = JsonSerializer.Serialize(new ReceiptEmailDeliveryDetails(request.Id, report.Status, ReceiptEmailDelivery.Hash(report.EventId), clock.UtcNow))
        };
        db.OrderHistoryEvents.Add(entry);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.Entry(entry).State = EntityState.Detached;
            if (!await db.OrderHistoryEvents.AsNoTracking().AnyAsync(item => item.Id == eventId && item.TenantId == tenantId && item.OrderId == request.OrderId, cancellationToken)) throw;
        }
    }
}
