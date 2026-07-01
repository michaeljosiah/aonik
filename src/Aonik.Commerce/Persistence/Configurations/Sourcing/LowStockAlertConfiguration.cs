using Aonik.Commerce.Entities.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Sourcing;

public class LowStockAlertConfiguration : IEntityTypeConfiguration<LowStockAlert>
{
    public void Configure(EntityTypeBuilder<LowStockAlert> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
        builder.Property(x => x.AvailableAtRaise).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.ReorderPoint).IsRequired().HasPrecision(19, 4);

        // At most one ACTIVE (Open/Acknowledged) alert per (tenant, ingredient) — Spec 052 §9/§10.
        // The SQL Server backstop behind the scan's active-alert check, so a concurrent or
        // double-fired scan cannot insert a duplicate. InMemory does not enforce filtered indexes —
        // the service invariant is covered by unit tests instead.
        builder.HasIndex(x => new { x.TenantId, x.IngredientId })
            .IsUnique()
            .HasFilter("[Status] IN (N'Open', N'Acknowledged')");

        // Admin list / agent reads: alerts by status, newest first.
        builder.HasIndex(x => new { x.TenantId, x.Status, x.RaisedAt });
    }
}
