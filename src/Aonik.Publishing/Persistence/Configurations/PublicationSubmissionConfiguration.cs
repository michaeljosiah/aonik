using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationSubmissionConfiguration : IEntityTypeConfiguration<PublicationSubmission>
{
    public void Configure(EntityTypeBuilder<PublicationSubmission> builder)
    {
        builder.ToTable("AnkPublicationSubmissions", "dbo");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.ChannelKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PublicationHoldReason).HasMaxLength(2000);
        // Permanent identity: soft deletion must not permit an old request key to be reused.
        builder.HasIndex(x => new { x.TenantId, x.CreatorPartyId, x.ClientRequestId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CreatorPartyId, x.CreatedAt });
    }
}
