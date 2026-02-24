using Aonik.Platform.Entities.Cms;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations.Cms;

internal class ContentBlockMediaConfiguration : IEntityTypeConfiguration<ContentBlockMedia>
{
    public void Configure(EntityTypeBuilder<ContentBlockMedia> builder)
    {
        builder.ToTable("ContentBlockMedia", SchemaNames.Default);
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContentBlockId)
            .IsRequired();

        builder.Property(x => x.StorageType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Alt)
            .HasMaxLength(200);

        builder.Property(x => x.Caption)
            .HasMaxLength(500);

        builder.Property(x => x.MimeType)
            .HasMaxLength(50);

        builder.Property(x => x.LinkUrl)
            .HasMaxLength(500);

        builder.Property(x => x.BlobContainer)
            .HasMaxLength(100);

        builder.Property(x => x.BlobPath)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.ContentBlockId, x.Order })
            .HasDatabaseName("IX_ContentBlockMedia_Order");
    }
}
