using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal sealed class CategorisationRuleConfiguration : IEntityTypeConfiguration<CategorisationRule>
{
    public void Configure(EntityTypeBuilder<CategorisationRule> builder)
    {
        builder.ToTable("CategorisationRules", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Pattern)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MatchType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Scope)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ApprovalStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.MinAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MaxAmount)
            .HasColumnType("decimal(18,2)");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Priority, x.IsActive });
    }
}
