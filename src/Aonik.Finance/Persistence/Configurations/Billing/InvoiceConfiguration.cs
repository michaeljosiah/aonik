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
        // Spec 088 §8 - one invoice per order, so a retried renewal cannot bill twice.
        // FILTERED because Invoice.OrderId is nullable and standalone invoices are valid: SQL
        // Server treats NULL as a value in a unique index and permits only one per tenant, so an
        // unfiltered index would reject every tenant's SECOND standalone invoice.
        builder.HasIndex(x => new { x.TenantId, x.OrderId })
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");
        // This replaces the previous non-unique (TenantId, OrderId) index rather than joining it:
        // EF collapses same-column declarations into one, and a filtered unique index still serves
        // every by-order lookup, since those always carry a non-null OrderId.

        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);

        // Same reasoning: every invoice written before this phase has a null key.
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.DueDate });

        // #223: covers the list endpoint's sort key — BillingService.ListInvoicesAsync
        // does OrderByDescending(IssueDate).ThenBy(Id) under the tenant query filter, so
        // (TenantId, IssueDate, Id) lets the DB serve the page order directly.
        builder.HasIndex(x => new { x.TenantId, x.IssueDate, x.Id });
    }
}
