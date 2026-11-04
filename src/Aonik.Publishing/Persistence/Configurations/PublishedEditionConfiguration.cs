using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublishedEditionConfiguration : IEntityTypeConfiguration<PublishedEdition>
{
    public void Configure(EntityTypeBuilder<PublishedEdition> b)
    {
        b.ToTable("AnkPublishedEditions", "dbo"); b.HasKey(x => x.Id);
        b.HasOne<PublicationCatalogueSnapshot>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChannelKey, x.SnapshotId })
            .HasPrincipalKey(x => new { x.TenantId, x.ChannelKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PublicationOperation>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChannelKey, x.OperationId })
            .HasPrincipalKey(x => new { x.TenantId, x.ChannelKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.ChannelKey, x.WorldId, x.Slot }).IsUnique().HasFilter("[IsCurrent] = 1");
        b.HasIndex(x => new { x.TenantId, x.OperationId });
        b.Property(x => x.ChannelKey).HasMaxLength(64);
        b.Property(x => x.WorldId).HasMaxLength(64);
        b.Property(x => x.Slot).HasMaxLength(150);
        b.Property(x => x.Kind).HasMaxLength(24);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.PackageSha256).HasMaxLength(64);
        b.Property(x => x.Fingerprint).HasMaxLength(64);
        b.Property(x => x.BaseFingerprint).HasMaxLength(64);
        b.Property(x => x.PinJson).HasMaxLength(65536);
    }
}
