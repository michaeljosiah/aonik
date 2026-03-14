using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal sealed class FinancialLifeGraphEdgeConfiguration : IEntityTypeConfiguration<FinancialLifeGraphEdge>
{
    public void Configure(EntityTypeBuilder<FinancialLifeGraphEdge> builder)
    {
        builder.ToTable("FinancialLifeGraphEdges", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromNodeKey)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.Predicate)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ToNodeKey)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.PropertiesJson)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.FromNodeKey, x.Predicate });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ToNodeKey, x.Predicate });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.HouseholdId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
    }
}
