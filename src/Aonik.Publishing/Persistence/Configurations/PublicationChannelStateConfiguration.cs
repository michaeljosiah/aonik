using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationChannelStateConfiguration : IEntityTypeConfiguration<PublicationChannelState>
{
    public void Configure(EntityTypeBuilder<PublicationChannelState> b)
    {
        b.ToTable("AnkPublicationChannels", "dbo"); b.HasKey(x => x.Id);
        b.HasOne<PublicationCatalogueSnapshot>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChannelKey, x.HeadSnapshotId })
            .HasPrincipalKey(x => new { x.TenantId, x.ChannelKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.ChannelKey }).IsUnique();
        b.Property(x => x.ChannelKey).HasMaxLength(64);
        b.Property(x => x.BaselineSha256).HasMaxLength(64);
        b.Property(x => x.PublicBaseUrl).HasMaxLength(2000);
        b.Property(x => x.MirrorStatus).HasMaxLength(32);
        b.Property(x => x.MirrorCode).HasMaxLength(100);
    }
}
