using Aonik.Finance.Entities.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Partners;

internal class ConnectorInstitutionCodeConfiguration : IEntityTypeConfiguration<ConnectorInstitutionCode>
{
    public void Configure(EntityTypeBuilder<ConnectorInstitutionCode> builder)
    {
        builder.ToTable("ConnectorInstitutionCodes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderInstitutionCode).IsRequired().HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.ConnectorId, x.FinancialInstitutionId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ConnectorId, x.ProviderInstitutionCode });

        builder.HasOne<Connector>().WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialInstitution>().WithMany().HasForeignKey(x => x.FinancialInstitutionId).OnDelete(DeleteBehavior.Restrict);
    }
}
