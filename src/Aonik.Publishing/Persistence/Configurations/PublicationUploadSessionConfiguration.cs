using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationUploadSessionConfiguration : IEntityTypeConfiguration<PublicationUploadSession>
{
    public void Configure(EntityTypeBuilder<PublicationUploadSession> b)
    {
        b.ToTable("AnkPublicationUploadSessions", "dbo"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.HasOne<SubmissionRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.SubmissionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.StorageLocationId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.DeclaredSha256).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.RevisionId });
        b.HasIndex(x => new { x.TenantId, x.CreatorPartyId, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt });
    }
}
