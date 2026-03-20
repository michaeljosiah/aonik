using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PeriodType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.BudgetCreatedBy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.UserId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.PeriodStart, x.Status });
    }
}
