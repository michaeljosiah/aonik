using Aonik.Documents.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Documents.Persistence.Configurations;

public class DocumentIngestionConfiguration : IEntityTypeConfiguration<DocumentIngestion>
{
    public void Configure(EntityTypeBuilder<DocumentIngestion> builder)
    {
        builder.ToTable("DocumentIngestions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.VectorCollection)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.EmbeddingModel)
            .HasMaxLength(100);

        builder.Property(i => i.EmbeddingCost)
            .HasColumnType("decimal(18,6)");

        builder.Property(i => i.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(i => i.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(i => i.DocumentId);
        builder.HasIndex(i => i.DocumentFileId);
        builder.HasIndex(i => i.Status);
    }
}
