using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal sealed class StatementImportRowConfiguration : IEntityTypeConfiguration<StatementImportRow>
{
    public void Configure(EntityTypeBuilder<StatementImportRow> builder)
    {
        builder.ToTable("StatementImportRows", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OccurredAtRaw)
            .HasMaxLength(100);

        builder.Property(x => x.AmountRaw)
            .HasMaxLength(100);

        builder.Property(x => x.DescriptionRaw)
            .HasMaxLength(500);

        builder.Property(x => x.MerchantRaw)
            .HasMaxLength(200);

        builder.Property(x => x.CurrencyRaw)
            .HasMaxLength(20);

        builder.Property(x => x.NormalizedCurrency)
            .HasMaxLength(3);

        builder.Property(x => x.NormalizedDescription)
            .HasMaxLength(500);

        builder.Property(x => x.ParseStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(x => x.Fingerprint)
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.StatementImportId, x.RowNumber })
            .IsUnique();

        builder.HasIndex(x => new { x.StatementImportId, x.ParseStatus });
        builder.HasIndex(x => new { x.TenantId, x.Fingerprint });
    }
}
