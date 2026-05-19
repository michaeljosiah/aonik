using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal sealed class FinancialLifeGraphNodeConfiguration : IEntityTypeConfiguration<FinancialLifeGraphNode>
{
    public void Configure(EntityTypeBuilder<FinancialLifeGraphNode> builder)
    {
        builder.ToTable("FinancialLifeGraphNodes", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NodeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SourceEntity)
            .HasMaxLength(100);

        builder.Property(x => x.PropertiesJson)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.NodeType });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.HouseholdId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SourceEntity, x.SourceId })
            .HasFilter("[SourceEntity] IS NOT NULL AND [SourceId] IS NOT NULL");
    }
}
