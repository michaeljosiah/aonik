using Aonik.Finance.Entities.ExternalAccounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.ExternalAccounts;

internal class ExternalAccountTransactionAttachmentConfiguration : IEntityTypeConfiguration<ExternalAccountTransactionAttachment>
{
    public void Configure(EntityTypeBuilder<ExternalAccountTransactionAttachment> builder)
    {
        builder.ToTable("ExternalAccountTransactionAttachments", SchemaNames.Default);

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

        builder.HasOne<ExternalAccountTransaction>()
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
