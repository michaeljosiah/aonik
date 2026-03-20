using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("BudgetLines", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LimitAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.HasIndex(x => x.BudgetId);
    }
}
