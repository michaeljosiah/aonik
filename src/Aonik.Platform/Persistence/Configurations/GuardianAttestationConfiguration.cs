using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class GuardianAttestationConfiguration : IEntityTypeConfiguration<GuardianAttestation>
{
    public void Configure(EntityTypeBuilder<GuardianAttestation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Method).IsRequired().HasMaxLength(48);
        builder.Property(x => x.EvidenceRef).HasMaxLength(256);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.RevocationReason).HasMaxLength(256);

        builder.HasIndex(x => new { x.TenantId, x.GuardianPartyId });

        // Deliberately NOT a filtered unique index on the active row: a guardian may legitimately
        // hold several attestations over time, and the verifier picks the current one. Uniqueness
        // here would make re-attesting after an expiry an error rather than the normal case.
        builder.HasIndex(x => new { x.TenantId, x.GuardianPartyId, x.ExpiresAt });
    }
}
