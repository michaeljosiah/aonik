using Aonik.Subscriptions.Entities.Entitlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Subscriptions.Persistence.Configurations;

public class EntitlementSigningKeyConfiguration : IEntityTypeConfiguration<EntitlementSigningKey>
{
    public void Configure(EntityTypeBuilder<EntitlementSigningKey> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kid).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Algorithm).IsRequired().HasMaxLength(16);
        builder.Property(x => x.PublicKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProtectedPrivateKey).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        // One kid per tenant. Two keys under one name would make verification depend on which row a
        // query happened to return.
        builder.HasIndex(x => new { x.TenantId, x.Kid }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class EntitlementTokenIssueConfiguration : IEntityTypeConfiguration<EntitlementTokenIssue>
{
    public void Configure(EntityTypeBuilder<EntitlementTokenIssue> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubscriberKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Kid).IsRequired().HasMaxLength(32);
        builder.Property(x => x.RevocationHandle).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DeviceFingerprint).HasMaxLength(128);

        builder.HasIndex(x => new { x.TenantId, x.Jti }).IsUnique().HasFilter("[IsDeleted] = 0");

        // §6.1: the true retirement date is MAX(GraceUntil) per kid, asked on every retirement check.
        builder.HasIndex(x => new { x.TenantId, x.Kid, x.GraceUntil });
        builder.HasIndex(x => new { x.TenantId, x.SubscriberKind, x.SubscriberId });
    }
}

public class EntitlementRevocationConfiguration : IEntityTypeConfiguration<EntitlementRevocation>
{
    public void Configure(EntityTypeBuilder<EntitlementRevocation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RevocationHandle).HasMaxLength(64);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(256);

        // The published-list read: everything not yet sweepable.
        builder.HasIndex(x => new { x.TenantId, x.SweepAfter });
    }
}
