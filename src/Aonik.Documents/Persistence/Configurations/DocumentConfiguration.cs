using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Documents.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.IssuerName)
            .HasMaxLength(200);

        builder.Property(d => d.CountryCode)
            .HasMaxLength(3);

        builder.Property(d => d.ReferenceNumber)
            .HasMaxLength(200);

        builder.Property(d => d.TagsJson)
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(d => d.AttributesJson)
            .IsRequired()
            .HasDefaultValue("{}");

        // Spec 035 — RAG/classification columns. Enums stored as int; existing rows
        // default to Internal/NotIndexable so legacy compliance docs are not auto-indexed.
        builder.Property(d => d.Classification)
            .HasDefaultValue(DocumentClassification.Internal);

        builder.Property(d => d.Source)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("AdminUpload");

        builder.Property(d => d.IndexStatus)
            .HasDefaultValue(DocumentIndexStatus.NotIndexable);

        builder.HasMany(d => d.Files)
            .WithOne(f => f.Document)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Spec 035 — the Document↔DocumentUsage FK is dropped: DocumentUsage (Compliance,
        // stays in Aonik.Platform) now references the document by a plain Guid resolved through
        // IDocumentReader, so there is no cross-module navigation/FK.
        builder.HasMany(d => d.Versions)
            .WithOne(v => v.Document)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.OwnerPartyId);
        builder.HasIndex(d => d.DocumentType);
        builder.HasIndex(d => d.Status);
    }
}
