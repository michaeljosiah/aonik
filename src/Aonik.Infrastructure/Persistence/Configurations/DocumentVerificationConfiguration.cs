using Aonik.Domain.Compliance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class DocumentVerificationConfiguration : IEntityTypeConfiguration<DocumentVerification>
{
    public void Configure(EntityTypeBuilder<DocumentVerification> builder)
    {
        builder.ToTable("DocumentVerifications");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Decision)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.DecisionReasonCode)
            .HasMaxLength(100);

        builder.Property(v => v.DecisionNotes)
            .HasMaxLength(1000);

        builder.Property(v => v.VerifierType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.VerifierId)
            .HasMaxLength(200);

        builder.HasIndex(v => v.DocumentUsageId);
        builder.HasIndex(v => v.VerifierType);
    }
}
