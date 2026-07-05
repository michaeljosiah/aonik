using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations.Accounts;

internal class AccountTransactionAttachmentConfiguration : IEntityTypeConfiguration<AccountTransactionAttachment>
{
    public void Configure(EntityTypeBuilder<AccountTransactionAttachment> builder)
    {
        builder.ToTable("AccountTransactionAttachments", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StorageProvider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.StorageContainer)
            .HasMaxLength(200);

        builder.Property(x => x.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Sha256)
            .HasMaxLength(64);

        builder.HasIndex(x => new { x.TenantId, x.TransactionId });

        builder.HasOne<AccountTransaction>()
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
