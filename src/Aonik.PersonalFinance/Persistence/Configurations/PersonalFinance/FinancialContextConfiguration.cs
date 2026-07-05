using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal class FinancialContextConfiguration : IEntityTypeConfiguration<FinancialContext>
{
    public void Configure(EntityTypeBuilder<FinancialContext> builder)
    {
        builder.ToTable("FinancialContexts", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ContextType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.MetadataJson)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasMany(x => x.FundingSources)
            .WithOne()
            .HasForeignKey(x => x.FinancialContextId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ContextType });
    }
}
