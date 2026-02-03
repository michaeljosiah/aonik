using Aonik.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

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

        builder.HasMany(d => d.Files)
            .WithOne(f => f.Document)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Usages)
            .WithOne(u => u.Document)
            .HasForeignKey(u => u.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Versions)
            .WithOne(v => v.Document)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.OwnerPartyId);
        builder.HasIndex(d => d.DocumentType);
        builder.HasIndex(d => d.Status);
    }
}
