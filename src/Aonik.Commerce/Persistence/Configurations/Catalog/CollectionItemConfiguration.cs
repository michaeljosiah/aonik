using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class CollectionItemConfiguration : IEntityTypeConfiguration<CollectionItem>
{
    public void Configure(EntityTypeBuilder<CollectionItem> builder)
    {
        builder.HasKey(x => x.Id);

        // Both unique indexes filter IsDeleted: AonikDbContextBase converts deletes into
        // soft-deleted rows, so an unfiltered unique index would keep a removed product's row
        // occupying the key and make re-adding it (or a delete/reinsert reorder) fail. Duplicate
        // ACTIVE ranks stay impossible — curated order stays deterministic (Spec 070 §5).
        builder.HasIndex(x => new { x.TenantId, x.CollectionId, x.ProductId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.CollectionId, x.Rank })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.CollectionId });

        // ProductId is a validated soft reference (authoring checks tenant membership), not an FK —
        // consistent with ProductOptionGroup; products soft-delete, so a hard FK buys nothing.
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
    }
}
