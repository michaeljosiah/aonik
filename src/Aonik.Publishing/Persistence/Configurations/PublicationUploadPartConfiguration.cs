using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence.Configurations;

public sealed class PublicationUploadPartConfiguration : IEntityTypeConfiguration<PublicationUploadPart>
{
    public void Configure(EntityTypeBuilder<PublicationUploadPart> b)
    {
        b.ToTable("AnkPublicationUploadParts", "dbo"); b.HasKey(x => x.Id);
        b.HasOne<PublicationUploadSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.SessionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.SessionId, x.Number }).IsUnique();
    }
}
