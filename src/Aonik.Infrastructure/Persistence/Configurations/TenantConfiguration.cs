using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Subdomain)
            .HasMaxLength(100);

        builder.Property(x => x.Environment)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DefaultCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.SupportedCountriesJson)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        // Unique index on subdomain (if present)
        builder.HasIndex(x => x.Subdomain)
            .IsUnique()
            .HasDatabaseName("IX_Tenant_Subdomain")
            .HasFilter("[Subdomain] IS NOT NULL");

        builder.HasIndex(x => x.Status);
    }
}
