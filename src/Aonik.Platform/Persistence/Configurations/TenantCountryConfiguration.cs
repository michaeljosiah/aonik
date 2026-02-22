using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class TenantCountryConfiguration : IEntityTypeConfiguration<TenantCountry>
{
    public void Configure(EntityTypeBuilder<TenantCountry> builder)
    {
        builder.ToTable("TenantCountries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CountryId)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CountryId })
            .IsUnique();
    }
}
