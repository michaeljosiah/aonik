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
    }
}
