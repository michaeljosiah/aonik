using Aonik.Platform.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Documents.Persistence.Configurations;

public class DocumentLinkConfiguration : IEntityTypeConfiguration<DocumentLink>
{
    public void Configure(EntityTypeBuilder<DocumentLink> builder)
    {
        builder.ToTable("DocumentLinks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetType)
            .IsRequired()
            .HasMaxLength(50);

        // "documents for target X" (profile + circle gate) and "links on document D".
        builder.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId });
        builder.HasIndex(x => new { x.TenantId, x.DocumentId });
    }
}
