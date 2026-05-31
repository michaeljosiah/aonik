using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class FinancialInstitutionConfiguration : IEntityTypeConfiguration<FinancialInstitution>
{
    public void Configure(EntityTypeBuilder<FinancialInstitution> builder)
    {
        builder.ToTable("FinancialInstitutions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.InstitutionType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.DefaultCurrency).HasMaxLength(3);
        builder.Property(x => x.Bic).HasMaxLength(11);

        builder.HasIndex(x => new { x.CountryCode, x.InstitutionType });
        builder.HasIndex(x => new { x.TenantId, x.CountryCode, x.Name });
    }
}
