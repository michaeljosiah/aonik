using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationCatalogueSnapshotConfiguration : IEntityTypeConfiguration<PublicationCatalogueSnapshot>
{
    public void Configure(EntityTypeBuilder<PublicationCatalogueSnapshot> b)
    {
        b.ToTable("AnkPublicationCatalogueSnapshots", "dbo"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.ChannelKey, x.Id });
        b.HasOne<PublicationOperation>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChannelKey, x.OperationId })
            .HasPrincipalKey(x => new { x.TenantId, x.ChannelKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PublicationCatalogueSnapshot>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChannelKey, x.PreviousSnapshotId })
            .HasPrincipalKey(x => new { x.TenantId, x.ChannelKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.ChannelKey, x.Serial }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        b.Property(x => x.ChannelKey).HasMaxLength(64);
        b.Property(x => x.Json).HasMaxLength(1048576);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.EvidenceReference).HasMaxLength(2000);
    }
}
