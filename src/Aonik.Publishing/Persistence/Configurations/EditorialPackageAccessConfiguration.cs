using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class EditorialPackageAccessConfiguration : IEntityTypeConfiguration<EditorialPackageAccess>
{
    public void Configure(EntityTypeBuilder<EditorialPackageAccess> b)
    {
        b.ToTable("AnkEditorialPackageAccesses", "dbo"); b.HasKey(x => x.Id);
        b.HasOne<SubmissionRevision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.SubmissionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.SubmissionId, x.AccessedAt });
        b.Property(x => x.PackageSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.AccessKind).HasMaxLength(20).HasDefaultValue("package").IsRequired();
        b.Property(x => x.AssetId).HasMaxLength(120);
    }
}
