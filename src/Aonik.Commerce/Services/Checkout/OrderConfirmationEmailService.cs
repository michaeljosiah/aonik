using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class OrderConfirmationEmailService(
    CommerceDbContext dbContext,
    ITenantProvider tenantProvider,
    IOrderService orders,
    ITemplatedEmailSender email,
    ILogger<OrderConfirmationEmailService> logger) : IOrderConfirmationEmailService
{
    public async Task SendAsync(Guid orderId, Guid completedPaymentIntentId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var summary = await dbContext.OrderChargeSummaries.AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == tenantId && row.OrderId == orderId, cancellationToken);
        if (summary is null || summary.PaymentIntentId != completedPaymentIntentId
            || summary.PaymentStatus != CheckoutPaymentStatuses.Captured) return;

        var cart = await dbContext.Carts.AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == tenantId && row.OrderId == orderId, cancellationToken);
        if (cart is null || cart.Status != CartStatuses.CheckedOut) return;

        var order = await orders.GetAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("The confirmed checkout order was not found.");
        if (order.TenantId != tenantId || order.Status != OrderStatusCodes.Complete) return;

        var delivery = await dbContext.OrderDeliveryDetails.AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == tenantId && row.OrderId == orderId, cancellationToken);
        if (delivery is null && cart.BoxBundleProductId is null)
        {
            // Legacy/generic checkout permits no delivery contact; never guess from an account email.
            logger.LogInformation("Order {OrderId} has no snapshotted purchaser contact; confirmation email is not applicable.", orderId);
            return;
        }
        if (delivery is null || string.IsNullOrWhiteSpace(delivery.PurchaserEmail))
            throw new InvalidOperationException("The box order is missing its purchaser contact snapshot.");

        var selections = await dbContext.OrderBundleSelections.AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.OrderId == orderId)
            .OrderBy(row => row.OrderItemIndex).ThenBy(row => row.Id)
            .Select(row => new { row.Sku, row.Quantity, row.PersonalisationSummary, row.NameSnapshot, row.IsSignatureSnapshot })
            .ToListAsync(cancellationToken);

        // Explicit dictionaries are Fluid's existing contract. No payment handles, raw metadata,
        // account data or live catalogue names enter the template model.
        var model = new Dictionary<string, object?>
        {
            ["order_id"] = order.Id.ToString("D"),
            ["order_number"] = order.OrderNumber,
            ["purchaser_name"] = $"{delivery.PurchaserFirstName} {delivery.PurchaserLastName}".Trim(),
            ["currency"] = summary.Currency,
            ["subtotal"] = Amount(summary.Subtotal),
            // Existing customised templates have one discount row; its total must still reconcile.
            ["discount_total"] = Amount(summary.DiscountTotal + summary.PointsAppliedValue),
            ["coupon_discount_total"] = Amount(summary.DiscountTotal),
            ["points_applied_value"] = Amount(summary.PointsAppliedValue),
            ["has_points_redemption"] = summary.PointsAppliedValue > 0m,
            ["tax_total"] = Amount(summary.TaxTotal),
            ["delivery_total"] = Amount(order.Items.Where(item => item.ItemType == CheckoutService.DeliveryFeeItemType).Sum(item => item.AmountIn)),
            ["total"] = Amount(summary.Total),
            ["items"] = order.Items.Where(item => item.ItemType != CheckoutService.DeliveryFeeItemType)
                .OrderBy(item => item.ItemIndex).Select(item => new Dictionary<string, object?>
                {
                    ["description"] = item.NameSnapshot ?? (string.IsNullOrWhiteSpace(item.Sku) ? item.ItemType : item.Sku),
                    ["quantity"] = item.Quantity?.ToString("0.############################", CultureInfo.InvariantCulture),
                    ["unit_price"] = item.UnitPrice is { } price ? Amount(price) : null,
                    ["total"] = Amount(item.AmountIn)
                }).ToList(),
            ["selections"] = selections.Select(item => new Dictionary<string, object?>
            {
                ["description"] = item.NameSnapshot ?? item.Sku,
                ["is_signature"] = item.IsSignatureSnapshot,
                ["quantity"] = item.Quantity.ToString("0.############################", CultureInfo.InvariantCulture),
                ["personalisation"] = item.PersonalisationSummary
            }).ToList(),
            ["delivery"] = new Dictionary<string, object?>
            {
                ["date"] = delivery.DeliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["timezone"] = delivery.Timezone,
                ["recipient_name"] = delivery.RecipientName,
                ["line1"] = delivery.AddressLine1,
                ["line2"] = delivery.AddressLine2,
                ["city"] = delivery.City,
                ["region"] = delivery.Region,
                ["postcode"] = delivery.Postcode,
                ["country_code"] = delivery.CountryCode,
                ["notes"] = delivery.Notes
            }
        };

        // Exceptions escape to the existing outbox retry. Inbox completion suppresses ordinary
        // redelivery; an external-send/DB-commit crash can still send a duplicate.
        await email.SendAsync(new TemplatedEmailMessage(
            TransactionalEmailTemplateNames.OrderConfirmation, delivery.PurchaserEmail, model), cancellationToken);
    }

    private static string Amount(decimal value) => value.ToString("0.00##########################", CultureInfo.InvariantCulture);
}
