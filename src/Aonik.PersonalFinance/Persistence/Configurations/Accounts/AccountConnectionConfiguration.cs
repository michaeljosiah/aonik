using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations.Accounts;

internal class AccountConnectionConfiguration : IEntityTypeConfiguration<AccountConnection>
{
    public void Configure(EntityTypeBuilder<AccountConnection> builder)
    {
        builder.ToTable("AccountConnections", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderConnectionReference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.InstitutionName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.InstitutionReference)
            .HasMaxLength(200);

        builder.Property(x => x.AutoSyncEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.SyncIntervalMinutes)
            .HasDefaultValue(360);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ConsentStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SecretReference)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SyncCursor)
            .HasMaxLength(500);

        builder.Property(x => x.LastSyncStatus)
            .HasMaxLength(100);

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.Provider, x.ProviderConnectionReference })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.AutoSyncEnabled, x.NextScheduledSyncAt });
    }
}
