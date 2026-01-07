using Aonik.Domain.Billing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceId)
            .IsRequired();

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Quantity)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.UnitPrice)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.LineTotal)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.HasIndex(x => x.InvoiceId);
    }
}
