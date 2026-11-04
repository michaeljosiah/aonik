using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class EditorialReviewSnapshotConfiguration : IEntityTypeConfiguration<EditorialReviewSnapshot>
{
    public void Configure(EntityTypeBuilder<EditorialReviewSnapshot> b)
    {
        b.ToTable("AnkEditorialReviewSnapshots", "dbo"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id });
        b.HasOne<PublicationValidationReport>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.ValidationReportId })
            .HasPrincipalKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.RevisionId, x.ReviewDigest }).IsUnique();
        b.Property(x => x.ReviewDigest).HasMaxLength(64).IsRequired();
        b.Property(x => x.MaterialJson).HasMaxLength(524288).IsRequired();
        b.Property(x => x.PolicyJson).HasMaxLength(16000).IsRequired();
    }
}
