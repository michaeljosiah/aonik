using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Aonik.Platform.Entities.Autonumbering;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class AutonumberProfileConfiguration : IEntityTypeConfiguration<AutonumberProfile>
{
    public void Configure(EntityTypeBuilder<AutonumberProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(profile => profile.PrefixTemplate)
            .HasMaxLength(100);

        builder.Property(profile => profile.SuffixTemplate)
            .HasMaxLength(100);

        builder.HasIndex(profile => new { profile.TenantId, profile.EntityType })
            .IsUnique();
    }
}
