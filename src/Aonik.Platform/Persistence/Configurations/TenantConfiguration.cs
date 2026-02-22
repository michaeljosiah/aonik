using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

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

        // Company Setup fields
        builder.Property(x => x.LogoUrl)
            .HasMaxLength(2000);

        builder.Property(x => x.Industry)
            .HasMaxLength(100);

        builder.Property(x => x.CompanySize)
            .HasMaxLength(50);

        builder.Property(x => x.Website)
            .HasMaxLength(500);

        // Contact fields
        builder.Property(x => x.ContactEmail)
            .HasMaxLength(255);

        builder.Property(x => x.ContactMobile)
            .HasMaxLength(50);

        // Address fields
        builder.Property(x => x.AddressLine1)
            .HasMaxLength(255);

        builder.Property(x => x.AddressLine2)
            .HasMaxLength(255);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.StateProvince)
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20);

        builder.Property(x => x.Country)
            .HasMaxLength(2);

        // Setup tracking
        builder.Property(x => x.IsSetupComplete)
            .HasDefaultValue(false);

        builder.Property(x => x.SetupStep)
            .HasDefaultValue(0);
    }
}
