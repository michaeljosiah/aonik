using Aonik.Finance.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerAccountId)
            .IsRequired();

        builder.Property(x => x.OrderId)
            .IsRequired(false);

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

        // Tenant-leading composites (M8): every read is implicitly WHERE TenantId = @t
        // (the global query filter), so lead each index with TenantId to match the real
        // predicate instead of forcing the DB to isolate the tenant's slice separately.
        builder.HasIndex(x => new { x.TenantId, x.CustomerAccountId });
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.DueDate });

        // #223: covers the list endpoint's sort key — BillingService.ListInvoicesAsync
        // does OrderByDescending(IssueDate).ThenBy(Id) under the tenant query filter, so
        // (TenantId, IssueDate, Id) lets the DB serve the page order directly.
        builder.HasIndex(x => new { x.TenantId, x.IssueDate, x.Id });
    }
}
