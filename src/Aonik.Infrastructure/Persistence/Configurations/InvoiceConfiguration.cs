using Aonik.Domain.Billing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.TotalAmount)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.IssuedUtc)
            .IsRequired();

        builder.Property(x => x.DueUtc)
            .IsRequired();

        builder.HasMany(x => x.LineItems)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.InvoiceNumber)
            .IsUnique();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.DueUtc);
    }
}
