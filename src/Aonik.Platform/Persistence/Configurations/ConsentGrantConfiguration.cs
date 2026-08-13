using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class ConsentGrantConfiguration : IEntityTypeConfiguration<ConsentGrant>
{
    public void Configure(EntityTypeBuilder<ConsentGrant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(64);
        builder.Property(x => x.TermsVersion).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Jurisdiction).IsRequired().HasMaxLength(2);
        builder.Property(x => x.VerificationMethod).IsRequired().HasMaxLength(48);
        builder.Property(x => x.VerificationRef).HasMaxLength(256);
        builder.Property(x => x.RevocationReason).HasMaxLength(64);

        builder.HasIndex(x => new { x.TenantId, x.SubjectPartyId });
        builder.HasIndex(x => new { x.TenantId, x.GrantedByPartyId });

        // Spec 095 §13. TermsVersion is DELIBERATELY absent from this key.
        //
        // Including it would let an active v1 and an active v2 coexist — and since IConsentReader
        // asks only about subject and purpose, it would find the stale v1 and authorise. A material
        // terms change would then not actually invalidate anything unless every caller independently
        // knew the current version, which is exactly the coupling the reader contract avoids.
        //
        // Filtered on RevokedAt so only ONE active grant per (subject, purpose) can exist. Granting a
        // new version revokes the prior one in the same transaction; this index makes a second active
        // row impossible rather than merely unlikely.
        builder.HasIndex(x => new { x.TenantId, x.SubjectPartyId, x.Purpose })
            .HasFilter("[RevokedAt] IS NULL")
            .IsUnique();
    }
}
