using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Platform.Entities.Integrations;

namespace Aonik.Platform.Persistence.Configurations.Integrations;

internal sealed class AutomationIdentityConfiguration : IEntityTypeConfiguration<AutomationIdentity>
{
    public void Configure(EntityTypeBuilder<AutomationIdentity> builder)
    {
        builder.Property(identity => identity.Name).HasMaxLength(160);
        builder.Property(identity => identity.Status).HasMaxLength(24);
        builder.Property(identity => identity.GrantsJson).HasColumnType("nvarchar(max)");
        builder.Property(identity => identity.PermissionsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(identity => new { identity.TenantId, identity.Status, identity.ExpiresAt });
    }
}
