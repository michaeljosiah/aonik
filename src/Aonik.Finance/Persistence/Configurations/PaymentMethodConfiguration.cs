using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerPartyId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderToken)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProviderCustomerRef)
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.Brand)
            .HasMaxLength(30);

        // Last four digits only — the column is deliberately too small to hold a PAN.
        builder.Property(x => x.Last4)
            .HasMaxLength(4);

        builder.Property(x => x.Label)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.TenantId, x.CustomerPartyId });
        builder.HasIndex(x => new { x.TenantId, x.Provider, x.ProviderToken });
    }
}
