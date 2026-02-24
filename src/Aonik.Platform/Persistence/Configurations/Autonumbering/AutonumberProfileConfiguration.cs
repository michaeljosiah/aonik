using Aonik.Platform.Entities.Autonumbering;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Autonumbering;

internal class AutonumberProfileConfiguration : IEntityTypeConfiguration<AutonumberProfile>
{
    public void Configure(EntityTypeBuilder<AutonumberProfile> builder)
    {
        builder.ToTable("AutonumberProfiles", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PrefixTemplate)
            .HasMaxLength(100);

        builder.Property(x => x.SuffixTemplate)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.TenantId, x.EntityType })
            .IsUnique();
    }
}
