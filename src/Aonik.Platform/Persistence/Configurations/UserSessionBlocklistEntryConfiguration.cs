using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class UserSessionBlocklistEntryConfiguration : IEntityTypeConfiguration<UserSessionBlocklistEntry>
{
    public void Configure(EntityTypeBuilder<UserSessionBlocklistEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.RevokedUtc).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.Property(x => x.ExpiresUtc).IsRequired();

        // Hot path: middleware lookup by (TenantId, UserId), newest first.
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.RevokedUtc })
            .HasDatabaseName("IX_UserSessionBlocklist_TenantId_UserId_RevokedUtc");

        // Maintenance: prune-by-expiry job.
        builder.HasIndex(x => x.ExpiresUtc)
            .HasDatabaseName("IX_UserSessionBlocklist_ExpiresUtc");
    }
}
