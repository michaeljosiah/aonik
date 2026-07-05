using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations.Accounts;

internal class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("AccountTransactions", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderTransactionReference)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Counterparty)
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Reference)
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.SubCategory)
            .HasMaxLength(100);

        builder.Property(x => x.CategoryMethod)
            .HasMaxLength(50);

        builder.Property(x => x.CategoryConfidence)
            .HasPrecision(5, 4);

        builder.Property(x => x.ReconciliationStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.AccountConnectionId, x.ProviderTransactionReference })
            .IsUnique()
            .HasFilter("[AccountConnectionId] IS NOT NULL");

        builder.HasIndex(x => new { x.TenantId, x.AccountId, x.ProviderTransactionReference })
            .IsUnique()
            .HasFilter("[AccountConnectionId] IS NULL");

        builder.HasIndex(x => new { x.TenantId, x.AccountId, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.ReconciliationStatus });

        builder.HasOne<AccountConnection>()
            .WithMany()
            .HasForeignKey(x => x.AccountConnectionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
