using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class FinancialConnectionConfiguration : IEntityTypeConfiguration<FinancialConnection>
{
    public void Configure(EntityTypeBuilder<FinancialConnection> builder)
    {
        builder.ToTable("FinancialConnections", SchemaNames.Default);

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

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Provider, x.ProviderConnectionReference })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
        builder.HasIndex(x => new { x.AutoSyncEnabled, x.NextScheduledSyncAt });
    }
}
