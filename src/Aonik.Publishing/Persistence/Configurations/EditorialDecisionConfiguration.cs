using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class EditorialDecisionConfiguration : IEntityTypeConfiguration<EditorialDecision>
{
    public void Configure(EntityTypeBuilder<EditorialDecision> b)
    {
        b.ToTable("AnkEditorialDecisions", "dbo"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id });
        b.HasOne<EditorialReviewSnapshot>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.SnapshotId })
            .HasPrincipalKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.SubmissionId, x.OperationId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.SubmissionId, x.DecidedAt });
        b.Property(x => x.CommandDigest).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReviewDigest).HasMaxLength(64).IsRequired();
        b.Property(x => x.ExpectedVersion).HasMaxLength(64).IsRequired();
        b.Property(x => x.Decision).HasMaxLength(32).IsRequired();
        b.Property(x => x.InternalReason).HasMaxLength(4000).IsRequired();
        b.Property(x => x.CreatorMessage).HasMaxLength(2000).IsRequired();
        b.Property(x => x.PolicyVersion).HasMaxLength(120).IsRequired();
        b.Property(x => x.ChecksJson).HasMaxLength(1000);
    }
}
