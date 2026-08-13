using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class ConsentVerificationConfiguration : IEntityTypeConfiguration<ConsentVerification>
{
    public void Configure(EntityTypeBuilder<ConsentVerification> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Method).IsRequired().HasMaxLength(48);
        builder.Property(x => x.OutcomeRef).HasMaxLength(256);
        builder.Property(x => x.FailureReason).HasMaxLength(256);

        // Keyed on the guardian, not the child: verification happens before the child exists, and for
        // a failed attempt the child never will. A pattern of failures is therefore per guardian —
        // which is the more interesting unit anyway (Spec 095 §12.2).
        builder.HasIndex(x => new { x.TenantId, x.GuardianPartyId });
        builder.HasIndex(x => new { x.TenantId, x.EnrolmentAttemptId });
        builder.HasIndex(x => new { x.TenantId, x.GuardianPartyId, x.Succeeded });
    }
}
