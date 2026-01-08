using Aonik.Domain.Billing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerAccountId)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Total)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.Subtotal)
            .HasPrecision(19, 4);

        builder.Property(x => x.TaxTotal)
            .HasPrecision(19, 4);

        builder.Property(x => x.DiscountTotal)
            .HasPrecision(19, 4);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.IssueDate)
            .IsRequired();

        builder.Property(x => x.DueDate)
            .IsRequired();

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CustomerAccountId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.DueDate);
    }
}
