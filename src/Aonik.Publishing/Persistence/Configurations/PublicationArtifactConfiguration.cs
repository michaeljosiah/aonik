using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationArtifactConfiguration : IEntityTypeConfiguration<PublicationArtifact>
{
    public void Configure(EntityTypeBuilder<PublicationArtifact> b)
    {
        b.ToTable("AnkPublicationArtifacts", "dbo"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.OperationId, x.AssetId }).IsUnique();
        b.HasOne<PublicationOperation>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChannelKey, x.OperationId })
            .HasPrincipalKey(x => new { x.TenantId, x.ChannelKey, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ChannelKey).HasMaxLength(64);
        b.Property(x => x.AssetId).HasMaxLength(120);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.MediaType).HasMaxLength(100);
        b.Property(x => x.StorageLocationId).HasMaxLength(64);
        b.Property(x => x.Url).HasMaxLength(2400);
    }
}
