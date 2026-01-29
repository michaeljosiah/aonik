using Aonik.Domain.Cms.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlocks");
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.ContentKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Slug)
            .HasMaxLength(200);

        builder.Property(x => x.Area)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Format)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Body)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Locale)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.TargetingJson)
            .HasColumnType("nvarchar(max)");

        builder.HasMany(x => x.Media)
            .WithOne()
            .HasForeignKey(x => x.ContentBlockId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ContentKey, x.Locale })
            .IsUnique()
            .HasDatabaseName("IX_ContentBlock_Tenant_Key_Locale");

        builder.HasIndex(x => new { x.TenantId, x.Area, x.IsEnabled, x.StartAt, x.EndAt, x.Priority })
            .HasDatabaseName("IX_ContentBlock_Query_Active");
    }
}
