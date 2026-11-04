using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class SubmissionRevisionConfiguration : IEntityTypeConfiguration<SubmissionRevision>
{
    public void Configure(EntityTypeBuilder<SubmissionRevision> builder)
    {
        builder.ToTable("AnkSubmissionRevisions", "dbo");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.SubmissionId, x.Id });
        builder.HasOne<PublicationSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SubmissionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.SubmissionId, x.Number }).IsUnique();
        builder.Property(x => x.RequestDigest).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatorDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(35).IsRequired();
        builder.Property(x => x.WorldId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Edition).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PackageFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.DeclaredPackageSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RightsDeclarationVersion).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EditorialMaterialJson).HasMaxLength(102400);
        builder.Property(x => x.RevisionCommandDigest).HasMaxLength(64);
        builder.HasIndex(x => new { x.TenantId, x.SubmissionId, x.RevisionOperationId }).IsUnique();
    }
}
