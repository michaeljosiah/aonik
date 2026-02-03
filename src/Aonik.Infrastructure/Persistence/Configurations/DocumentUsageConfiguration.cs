using Aonik.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class DocumentUsageConfiguration : IEntityTypeConfiguration<DocumentUsage>
{
    public void Configure(EntityTypeBuilder<DocumentUsage> builder)
    {
        builder.ToTable("DocumentUsages");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Purpose)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Notes)
            .HasMaxLength(1000);

        builder.HasMany(u => u.Verifications)
            .WithOne(v => v.DocumentUsage)
            .HasForeignKey(v => v.DocumentUsageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => u.DocumentId);
        builder.HasIndex(u => u.Purpose);
        builder.HasIndex(u => u.Status);
    }
}
