using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class LegacyConsentConfiguration : IEntityTypeConfiguration<LegacyConsent>
{
    public void Configure(EntityTypeBuilder<LegacyConsent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConsentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceBundleVersion).HasMaxLength(32);

        builder.HasIndex(x => new { x.TenantId, x.PartyId });
        builder.HasIndex(x => new { x.TenantId, x.OriginalConsentId });
    }
}
