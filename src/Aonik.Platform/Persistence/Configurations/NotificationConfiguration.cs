using Aonik.Platform.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Body)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ActionUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(200);

        builder.Property(x => x.MetadataJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status, x.CreatedAt })
            .HasDatabaseName("IX_Notification_Tenant_User_Status_CreatedAt");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_Notification_Tenant_User_CreatedAt");
    }
}
