using Aonik.Platform.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class NotificationDeviceConfiguration : IEntityTypeConfiguration<NotificationDevice>
{
    public void Configure(EntityTypeBuilder<NotificationDevice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Platform)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.DeviceToken)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.LastSeenAtUtc)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.Provider, x.Platform, x.DeviceToken })
            .IsUnique()
            .HasDatabaseName("IX_NotificationDevice_Tenant_Provider_Platform_Token");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Platform })
            .HasDatabaseName("IX_NotificationDevice_Tenant_User_Platform");
    }
}
