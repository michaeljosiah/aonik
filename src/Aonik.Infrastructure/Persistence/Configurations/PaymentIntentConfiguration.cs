using Aonik.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Reference)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedUtc)
            .IsRequired();

        builder.HasIndex(x => x.Reference);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedUtc);
    }
}
