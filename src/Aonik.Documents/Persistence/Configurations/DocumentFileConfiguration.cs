using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Documents.Persistence.Configurations;

public class DocumentFileConfiguration : IEntityTypeConfiguration<DocumentFile>
{
    public void Configure(EntityTypeBuilder<DocumentFile> builder)
    {
        builder.ToTable("DocumentFiles");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.StorageProvider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(f => f.StorageContainer)
            .HasMaxLength(200);

        builder.Property(f => f.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.FileName)
            .HasMaxLength(260);

        builder.Property(f => f.Sha256)
            .HasMaxLength(128);

        builder.Property(f => f.Side)
            .HasMaxLength(20);

        builder.Property(f => f.CapturedBy)
            .HasMaxLength(200);

        builder.Property(f => f.MetadataJson)
            .IsRequired()
            .HasDefaultValue("{}");

        // Spec 035 §9 — embeddable-text availability (enum stored as int).
        builder.Property(f => f.ExtractedTextStatus)
            .HasDefaultValue(ExtractedTextStatus.Unsupported);

        builder.HasIndex(f => f.DocumentId);
    }
}
