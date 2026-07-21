using Aonik.Commerce.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Commerce.Persistence.Configurations.Catalog;

public class OptionGroupConfiguration : IEntityTypeConfiguration<OptionGroup>
{
    public void Configure(EntityTypeBuilder<OptionGroup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(128);
        builder.Property(x => x.HelpText).HasMaxLength(512);
        builder.Property(x => x.SelectionMode).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.HasMany(x => x.Choices)
            .WithOne()
            .HasForeignKey(x => x.OptionGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Filtered on IsDeleted so a soft-deleted group does not permanently occupy its key.
        builder.HasIndex(x => new { x.TenantId, x.Key })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
