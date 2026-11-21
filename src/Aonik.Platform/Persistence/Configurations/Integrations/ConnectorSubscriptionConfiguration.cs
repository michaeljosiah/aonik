using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Aonik.Platform.Entities.Integrations;

namespace Aonik.Platform.Persistence.Configurations.Integrations;

internal sealed class ConnectorSubscriptionConfiguration : IEntityTypeConfiguration<ConnectorSubscription>
{
    public void Configure(EntityTypeBuilder<ConnectorSubscription> builder)
    {
        builder.Property(value => value.ConnectorKey).HasMaxLength(80);
        builder.Property(value => value.ApplicationKey).HasMaxLength(64);
        builder.Property(value => value.ProviderAccountHash).HasMaxLength(64);
        builder.Property(value => value.WatchClaim).HasMaxLength(64);
        builder.Property(value => value.Status).HasMaxLength(32);
        builder.Property(value => value.Mode).HasMaxLength(32);
        builder.Property(value => value.LastError).HasMaxLength(80);
        builder.Property(value => value.HistoryId).HasMaxLength(40);
        builder.Property(value => value.TargetHistoryId).HasMaxLength(40);
        builder.Property(value => value.PageToken).HasMaxLength(4096);
        builder.Property(value => value.BindingsJson).HasColumnType("nvarchar(max)");
        builder.Property(value => value.PollTaskProof).HasColumnType("nvarchar(max)");
        builder.HasIndex(value => new { value.TenantId, value.ConnectionId }).IsUnique();
        // Gmail permits one watch per provider account/application, including across local tenants.
        builder.HasIndex(value => value.WatchClaim).IsUnique().HasFilter("[WatchClaim] IS NOT NULL");
        builder.HasIndex(value => new { value.ApplicationKey, value.ProviderAccountHash, value.Status });
    }
}
