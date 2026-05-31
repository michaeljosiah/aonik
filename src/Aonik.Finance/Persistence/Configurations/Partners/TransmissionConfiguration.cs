using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class TransmissionConfiguration : IEntityTypeConfiguration<Transmission>
{
    public void Configure(EntityTypeBuilder<Transmission> builder)
    {
        // Exactly one target FK is set per attempt; the typed nullable FKs below keep the referential
        // integrity that a polymorphic TargetType + TargetId pair would forfeit. The later Map* call
        // overrides the table name to the Ank-prefixed form, but the check constraint added here survives.
        builder.ToTable("Transmissions", t => t.HasCheckConstraint(
            "CK_Transmissions_ExactlyOneTarget",
            "(CASE WHEN [PayoutId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PaymentIntentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PartnerBillPaymentId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.RawResponseJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.ConnectorId });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey });

        // ConnectorId stays a soft reference (no FK): it mirrors Payout.ConnectorId and avoids an FK
        // violation on any legacy transmission whose ConnectorId predates the connector catalog. EF
        // auto-indexes the three target FK columns below, so they need no explicit HasIndex.
        builder.HasOne<Payout>().WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentIntent>().WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PartnerBillPayment>().WithMany().HasForeignKey(x => x.PartnerBillPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
