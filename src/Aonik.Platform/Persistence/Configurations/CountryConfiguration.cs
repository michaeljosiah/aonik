using Aonik.Platform.Entities.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IsoAlpha2)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.IsoAlpha3)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.IsoNumeric);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.TenantId);

        builder.HasIndex(x => x.IsoAlpha2)
            .IsUnique();
    }
}
