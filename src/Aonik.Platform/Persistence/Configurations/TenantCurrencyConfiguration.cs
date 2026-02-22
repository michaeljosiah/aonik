using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class TenantCurrencyConfiguration : IEntityTypeConfiguration<TenantCurrency>
{
    public void Configure(EntityTypeBuilder<TenantCurrency> builder)
    {
        builder.ToTable("TenantCurrencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CurrencyId)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CurrencyId })
            .IsUnique();
    }
}
