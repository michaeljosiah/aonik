using Aonik.Finance.Entities.Accounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Accounts;

internal class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AccountSubtype)
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Country)
            .HasMaxLength(3);

        builder.Property(x => x.MaskedIdentifier)
            .HasMaxLength(50);

        builder.Property(x => x.InstitutionName)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.ProviderAccountReference)
            .HasMaxLength(200);

        builder.Property(x => x.LastSyncStatus)
            .HasMaxLength(100);

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.AccountConnectionId, x.ProviderAccountReference })
            .IsUnique()
            .HasFilter("[AccountConnectionId] IS NOT NULL");

        builder.HasIndex(x => new { x.TenantId, x.AccountType });

        builder.HasOne<AccountConnection>()
            .WithMany()
            .HasForeignKey(x => x.AccountConnectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
