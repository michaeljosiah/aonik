using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Fulfilment;

internal sealed class OrderFulfilmentService(CommerceDbContext db, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IClock clock, IOrderService orders) : IOrderFulfilmentService
{
    private static readonly string[] Stages = [OrderFulfilmentStatuses.Confirmed, OrderFulfilmentStatuses.Cooking,
        OrderFulfilmentStatuses.OutForDelivery, OrderFulfilmentStatuses.Delivered];

    public async Task<OrderFulfilmentDto> UpdateAsync(Guid orderId, UpdateOrderFulfilmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TryGetCurrentUserId(out var actorId) || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated staff user is required.");
        var nextIndex = Array.IndexOf(Stages, command.Status);
        if (nextIndex < 0 || command.ExpectedVersion is null)
            throw new StorefrontValidationException("Choose a known fulfilment stage and supply the current version.");
        byte[] version;
        try { version = Convert.FromBase64String(command.ExpectedVersion); }
        catch (FormatException) { throw new StorefrontValidationException("The fulfilment version must be valid base64."); }

        var delivery = await LoadPaidAsync(orderId, cancellationToken);
        var current = delivery.FulfilmentStatus ?? OrderFulfilmentStatuses.Confirmed;
        if (current == command.Status) return OrderFulfilmentData.Map(delivery);
        if (!version.SequenceEqual(delivery.RowVersion)) throw new OrderFulfilmentConflictException();
        var currentIndex = Array.IndexOf(Stages, current);
        if (currentIndex < 0 || nextIndex != currentIndex + 1)
            throw new StorefrontValidationException("Fulfilment must advance one stage at a time.");

        var history = OrderFulfilmentData.ReadHistory(delivery);
        history.Add(new OrderFulfilmentEvent(current, command.Status, actorId, clock.UtcNow));
        delivery.FulfilmentHistoryJson = OrderFulfilmentData.Serialize(history);
        delivery.FulfilmentStatus = command.Status;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return OrderFulfilmentData.Map(delivery);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(delivery).State = EntityState.Detached;
            var winner = await LoadPaidAsync(orderId, cancellationToken);
            if (winner.FulfilmentStatus == command.Status) return OrderFulfilmentData.Map(winner);
            throw new OrderFulfilmentConflictException();
        }
        catch
        {
            db.Entry(delivery).State = EntityState.Detached;
            throw;
        }
    }

    private async Task<OrderDeliveryDetails> LoadPaidAsync(Guid orderId, CancellationToken ct)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        foreach (var entry in db.ChangeTracker.Entries<OrderDeliveryDetails>()
            .Where(entry => entry.Entity.TenantId == tenantId && entry.Entity.OrderId == orderId).ToArray())
            entry.State = EntityState.Detached;
        if (!await db.Carts.AsNoTracking().AnyAsync(cart => cart.TenantId == tenantId && cart.OrderId == orderId, ct))
            throw new NotFoundException("The storefront order was not found.");
        var summary = await db.OrderChargeSummaries.AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == tenantId && row.OrderId == orderId, ct);
        var order = await orders.GetAsync(orderId, ct);
        if (summary is null || order is null || order.TenantId != tenantId)
            throw new NotFoundException("The storefront order was not found.");
        if (summary.PaymentStatus != CheckoutPaymentStatuses.Captured
            || OrderFulfilmentData.Status(null, summary.PaymentStatus, order.Status) == "Cancelled")
            throw new StorefrontValidationException("Only confirmed paid orders that have not been cancelled can advance fulfilment.");
        return await db.OrderDeliveryDetails.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.OrderId == orderId, ct)
            ?? throw new NotFoundException("The order has no recorded delivery to fulfil.");
    }
}
