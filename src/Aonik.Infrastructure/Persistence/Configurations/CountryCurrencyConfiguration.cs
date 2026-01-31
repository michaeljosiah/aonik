using Aonik.Domain.ReferenceData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class CountryCurrencyConfiguration : IEntityTypeConfiguration<CountryCurrency>
{
    public void Configure(EntityTypeBuilder<CountryCurrency> builder)
    {
        builder.ToTable("CountryCurrencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CountryId)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.HasIndex(x => new { x.CountryId, x.CurrencyCode })
            .IsUnique();

        builder.HasIndex(x => new { x.CountryId, x.IsDefault })
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }
}
