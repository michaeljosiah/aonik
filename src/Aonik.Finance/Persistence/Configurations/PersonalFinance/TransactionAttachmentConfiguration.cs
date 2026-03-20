using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal sealed class TransactionAttachmentConfiguration : IEntityTypeConfiguration<TransactionAttachment>
{
    public void Configure(EntityTypeBuilder<TransactionAttachment> builder)
    {
        builder.ToTable("TransactionAttachments", SchemaNames.Default);

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
            .HasMaxLength(260);

        builder.Property(x => x.Sha256)
            .HasMaxLength(128);

        builder.HasIndex(x => new { x.TenantId, x.TransactionId });

        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.Attachments)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
