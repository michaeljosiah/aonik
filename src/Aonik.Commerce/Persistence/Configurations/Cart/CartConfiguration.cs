using Aonik.Commerce.Entities.Cart;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Cart;

public class CartConfiguration : IEntityTypeConfiguration<Entities.Cart.Cart>
{
    public void Configure(EntityTypeBuilder<Entities.Cart.Cart> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.AnonymousToken).HasMaxLength(128);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.BuyerPartyId });
        builder.HasIndex(x => x.AnonymousToken);
        builder.HasIndex(x => x.OrderId);
    }
}
