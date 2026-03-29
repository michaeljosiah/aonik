using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class PersonalLinkedAccountConfiguration : IEntityTypeConfiguration<PersonalLinkedAccount>
{
    public void Configure(EntityTypeBuilder<PersonalLinkedAccount> builder)
    {
        builder.ToTable("PersonalLinkedAccounts", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderAccountReference)
            .IsRequired()
            .HasMaxLength(200);

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

        builder.Property(x => x.Last4)
            .HasMaxLength(4);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.LastSyncStatus)
            .HasMaxLength(100);

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        builder.HasIndex(x => x.PersonalAccountId)
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.FinancialConnectionId, x.ProviderAccountReference })
            .IsUnique();

        builder.HasOne<FinancialConnection>()
            .WithMany()
            .HasForeignKey(x => x.FinancialConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PersonalAccount>()
            .WithMany()
            .HasForeignKey(x => x.PersonalAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
