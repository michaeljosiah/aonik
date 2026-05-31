using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Orders;

internal class OrderFulfilmentRefConfiguration : IEntityTypeConfiguration<OrderFulfilmentRef>
{
    public void Configure(EntityTypeBuilder<OrderFulfilmentRef> builder)
    {
        // A payout, a partner collection (PaymentIntent), or a bill / airtime payment can each fulfil an
        // order: exactly one typed target FK is set per row, enforced by the check constraint. The later
        // Map* call overrides the table name to the Ank-prefixed form; the check constraint survives.
        builder.ToTable("OrderFulfilmentRefs", t => t.HasCheckConstraint(
            "CK_OrderFulfilmentRefs_ExactlyOneTarget",
            "(CASE WHEN [PayoutId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PaymentIntentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PartnerBillPaymentId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.TenantId, x.OrderId });

        // EF auto-indexes the four FK columns below, so they need no explicit HasIndex.
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payout>().WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentIntent>().WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PartnerBillPayment>().WithMany().HasForeignKey(x => x.PartnerBillPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
