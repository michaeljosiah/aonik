using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Aonik.Domain.Notifications.Entities;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class NotificationTemplateBindingConfiguration : IEntityTypeConfiguration<NotificationTemplateBinding>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateBinding> builder)
    {
        builder.ToTable("NotificationTemplateBindings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.TemplateName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.TemplateName, x.Channel })
            .IsUnique()
            .HasDatabaseName("IX_NotificationTemplateBinding_Tenant_Name_Channel");

        builder.HasOne<NotificationTemplate>()
            .WithMany()
            .HasForeignKey(x => x.BaseTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<NotificationTemplate>()
            .WithMany()
            .HasForeignKey(x => x.OverrideTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
