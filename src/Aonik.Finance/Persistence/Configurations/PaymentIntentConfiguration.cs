using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.ToTable("PaymentIntents", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.OrderId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PurposeType)
            .IsRequired()
            .HasMaxLength(50);

        // Nullable: a draft intent may not yet have a resolved rail. A concrete
        // method is enforced by PaymentService at the authorize (money-movement) step.
        builder.Property(x => x.PaymentMethodType)
            .HasMaxLength(50);

        // ── Partner-collection linkage (spec 031) ───────────────────────────
        // All nullable: a card-checkout intent leaves them unset; a partner-initiated collection fills
        // them. The partner outcome rides CollectionStatus, never the PaymentStatus-typed Status column.
        builder.Property(x => x.ClientReference).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.CollectionMethod).HasMaxLength(30);
        builder.Property(x => x.MobileNetwork).HasMaxLength(50);
        builder.Property(x => x.MaskedPhoneNumber).HasMaxLength(50);
        builder.Property(x => x.NextActionMode).HasMaxLength(30);
        builder.Property(x => x.NextActionRedirectUrl).HasMaxLength(2000);
        builder.Property(x => x.NextActionUssdCode).HasMaxLength(50);
        builder.Property(x => x.CollectionStatus).HasMaxLength(50);
        builder.Property(x => x.SettledAmount).HasPrecision(19, 4);
        builder.Property(x => x.Fee).HasPrecision(19, 4);
        builder.Property(x => x.FxRate).HasPrecision(19, 8);

        // Tenant-leading composites (M8) — match the implicit WHERE TenantId = @t filter
        // (the partner-reference indexes below are already tenant-leading).
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.PayerPartyId });
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
        builder.HasIndex(x => new { x.TenantId, x.InvoiceId });
        builder.HasIndex(x => new { x.TenantId, x.ClientReference });
        builder.HasIndex(x => new { x.TenantId, x.ProviderReference });
        builder.HasIndex(x => new { x.TenantId, x.ConnectorId });
    }
}
