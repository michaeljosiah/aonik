using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.ClientReference).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.RawResponseJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.PaymentId });
        builder.HasIndex(x => new { x.TenantId, x.PaymentIntentId });

        // PaymentId (card Payment) and PaymentIntentId (partner-collection) are soft references - a refund
        // points at exactly one of them - and ConnectorId is likewise soft, so none is a hard FK.
    }
}
