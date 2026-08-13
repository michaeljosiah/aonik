using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class ConsentTermsVersionConfiguration : IEntityTypeConfiguration<ConsentTermsVersion>
{
    public void Configure(EntityTypeBuilder<ConsentTermsVersion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version).IsRequired().HasMaxLength(32);
        builder.Property(x => x.NamedProviders).IsRequired().HasMaxLength(1024);

        builder.HasIndex(x => new { x.TenantId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsCurrent });
    }
}
