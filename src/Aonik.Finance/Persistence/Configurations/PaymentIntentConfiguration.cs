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

        builder.Property(x => x.PaymentMethodType)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PayerPartyId);
        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.InvoiceId);
    }
}
