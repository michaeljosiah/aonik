using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal sealed class PersonalTransactionConfiguration : IEntityTypeConfiguration<PersonalTransaction>
{
    public void Configure(EntityTypeBuilder<PersonalTransaction> builder)
    {
        builder.ToTable("PersonalTransactions", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Merchant)
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.TransactionType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.SubCategory)
            .HasMaxLength(200);

        builder.Property(x => x.CategorisedBy)
            .HasMaxLength(50);

        builder.Property(x => x.ClassificationMethod)
            .HasMaxLength(100);

        builder.Property(x => x.ClassifierVersion)
            .HasMaxLength(100);

        builder.Property(x => x.ReviewStatus)
            .HasMaxLength(50);

        builder.Property(x => x.ImportFingerprint)
            .HasMaxLength(200);

        builder.Property(x => x.TagsJson)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Category, x.OccurredAt });
        builder.HasIndex(x => new { x.PersonalAccountId, x.OccurredAt });

        builder.HasIndex(x => x.ImportFingerprint)
            .IsUnique()
            .HasFilter("[ImportFingerprint] IS NOT NULL");
    }
}
