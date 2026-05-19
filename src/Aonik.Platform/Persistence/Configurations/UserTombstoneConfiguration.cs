using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class UserTombstoneConfiguration : IEntityTypeConfiguration<UserTombstone>
{
    public void Configure(EntityTypeBuilder<UserTombstone> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.OriginalUserId).IsRequired();
        builder.Property(x => x.DeletedUtc).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.MaskedEmail).HasMaxLength(320);

        builder.HasIndex(x => new { x.TenantId, x.DeletedUtc })
            .HasDatabaseName("IX_UserTombstones_TenantId_DeletedUtc");

        builder.HasIndex(x => x.OriginalUserId)
            .HasDatabaseName("IX_UserTombstones_OriginalUserId");
    }
}
