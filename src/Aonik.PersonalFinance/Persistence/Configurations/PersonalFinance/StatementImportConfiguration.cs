using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal sealed class StatementImportConfiguration : IEntityTypeConfiguration<StatementImport>
{
    public void Configure(EntityTypeBuilder<StatementImport> builder)
    {
        builder.ToTable("StatementImports", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(x => x.StorageUri)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Format)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.PersonalAccountId, x.CreatedAt });
    }
}
