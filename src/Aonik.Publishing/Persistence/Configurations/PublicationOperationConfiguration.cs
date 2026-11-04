using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationOperationConfiguration : IEntityTypeConfiguration<PublicationOperation>
{
    public void Configure(EntityTypeBuilder<PublicationOperation> b)
    {
        b.ToTable("AnkPublicationOperations", "dbo"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.ChannelKey, x.Id });
        b.HasOne<EditorialDecision>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.DecisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.ClientOperationId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.ChannelKey, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.SubmissionId });
        b.Property(x => x.ChannelKey).HasMaxLength(64);
        b.Property(x => x.CommandDigest).HasMaxLength(64);
        b.Property(x => x.Kind).HasMaxLength(24);
        b.Property(x => x.ReviewDigest).HasMaxLength(64);
        b.Property(x => x.ExpectedVersion).HasMaxLength(64);
        b.Property(x => x.ExpectedBaseFingerprint).HasMaxLength(64);
        b.Property(x => x.ExpectedSlotFingerprint).HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.Property(x => x.PolicyJson).HasMaxLength(16000);
        b.Property(x => x.Status).HasMaxLength(32);
        b.Property(x => x.Code).HasMaxLength(100);
    }
}
