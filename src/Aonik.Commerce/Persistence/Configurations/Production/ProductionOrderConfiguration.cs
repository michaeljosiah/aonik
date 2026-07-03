using Aonik.Commerce.Entities.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Production;

public class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Notes).HasMaxLength(1024);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // "This week's runs by state" — the operational board read (Spec 056 §8).
        builder.HasIndex(x => new { x.TenantId, x.Status, x.PlannedFor });
    }
}
