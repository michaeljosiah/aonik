using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Aonik.Domain.Notifications.Entities;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SubjectTemplate)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.BodyTemplate)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsShared)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Name, x.Channel })
            .IsUnique()
            .HasDatabaseName("IX_NotificationTemplate_Tenant_Name_Channel");
    }
}
