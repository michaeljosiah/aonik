using Aonik.Platform.Entities.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Platform.Persistence.Configurations;

public sealed class AccountAccessActionConfiguration : IEntityTypeConfiguration<AccountAccessAction>
{
    public void Configure(EntityTypeBuilder<AccountAccessAction> builder)
    {
        builder.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.OriginalEmail).HasMaxLength(320);
        builder.Property(x => x.ExternalIssuer).HasMaxLength(500);
        builder.Property(x => x.ExternalSubject).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Purpose, x.PaymentIntentId }).IsUnique()
            .HasFilter("[PaymentIntentId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.Email, x.SendWindowStartedAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Purpose, x.Status });
    }
}
