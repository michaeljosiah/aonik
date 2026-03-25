using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class FinancialContextFundingSourceConfiguration : IEntityTypeConfiguration<FinancialContextFundingSource>
{
    public void Configure(EntityTypeBuilder<FinancialContextFundingSource> builder)
    {
        builder.ToTable("FinancialContextFundingSources", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IsPrimary)
            .HasDefaultValue(false);

        builder.HasIndex(x => new { x.FinancialContextId, x.PersonalAccountId })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.FinancialContextId });
    }
}
