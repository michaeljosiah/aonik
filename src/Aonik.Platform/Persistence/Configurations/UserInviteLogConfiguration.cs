using Aonik.Platform.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class UserInviteLogConfiguration : IEntityTypeConfiguration<UserInviteLog>
{
    public void Configure(EntityTypeBuilder<UserInviteLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SentUtc).IsRequired();
        builder.Property(x => x.TokenPrefix).IsRequired().HasMaxLength(16);
        builder.Property(x => x.ExpiresUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SentUtc })
            .HasDatabaseName("IX_UserInviteLog_TenantId_UserId_SentUtc");
    }
}
