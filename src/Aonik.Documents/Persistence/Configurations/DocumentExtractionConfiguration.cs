using Aonik.Documents.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Documents.Persistence.Configurations;

public class DocumentExtractionConfiguration : IEntityTypeConfiguration<DocumentExtraction>
{
    public void Configure(EntityTypeBuilder<DocumentExtraction> builder)
    {
        builder.ToTable("DocumentExtractions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExtractionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.OutputJson)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.HasIndex(e => e.DocumentId);
        builder.Property(e => e.InputHash).HasMaxLength(64);
        builder.Property(e => e.Status).HasMaxLength(32).HasDefaultValue("Completed");
        builder.HasIndex(e => new { e.TenantId, e.DocumentId, e.ExtractionType, e.InputHash })
            .IsUnique().HasFilter("[InputHash] IS NOT NULL");
    }
}
