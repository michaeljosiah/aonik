using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationValidationReportConfiguration : IEntityTypeConfiguration<PublicationValidationReport>
{
    public void Configure(EntityTypeBuilder<PublicationValidationReport> b)
    {
        b.ToTable("AnkPublicationValidationReports", "dbo"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id });
        b.HasOne<SubmissionRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.RevisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.SubmissionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.RevisionId, x.OperationId }).IsUnique();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.PackageSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReaderCommit).HasMaxLength(40).IsRequired();
        b.Property(x => x.VerifierImage).HasMaxLength(300).IsRequired();
        b.Property(x => x.PolicyJson).HasMaxLength(8000).IsRequired();
        b.Property(x => x.ResultJson).HasMaxLength(PackageCapacity.MaximumReportBytes).IsRequired();
    }
}
