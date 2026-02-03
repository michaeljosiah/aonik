using Aonik.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Version)
            .IsRequired();

        builder.Property(v => v.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.DecisionReason)
            .HasMaxLength(500);

        builder.HasIndex(v => v.DocumentId);
        builder.HasIndex(v => v.Version);
    }
}
