using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationNamespaceConfiguration : IEntityTypeConfiguration<PublicationNamespace>
{
    public void Configure(EntityTypeBuilder<PublicationNamespace> b)
    {
        b.ToTable("AnkPublicationNamespaces", "dbo"); b.HasKey(x => x.Id);
        b.Property(x => x.ChannelKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.WorldId).HasMaxLength(64).IsRequired();
        b.Property(x => x.ApprovedBaseFingerprint).HasMaxLength(64);
        b.HasIndex(x => new { x.TenantId, x.ChannelKey, x.WorldId }).IsUnique();
    }
}
