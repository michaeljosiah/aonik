using Aonik.Finance.Entities.ExternalAccounts;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.ExternalAccounts;

internal class ExternalAccountTransactionConfiguration : IEntityTypeConfiguration<ExternalAccountTransaction>
{
    public void Configure(EntityTypeBuilder<ExternalAccountTransaction> builder)
    {
        builder.ToTable("ExternalAccountTransactions", SchemaNames.Default);

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

        builder.Property(x => x.ReconciliationStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.ExternalAccountConnectionId, x.ProviderTransactionReference })
            .IsUnique()
            .HasFilter("[ExternalAccountConnectionId] IS NOT NULL");

        builder.HasIndex(x => new { x.TenantId, x.ExternalAccountId, x.ProviderTransactionReference })
            .IsUnique()
            .HasFilter("[ExternalAccountConnectionId] IS NULL");

        builder.HasIndex(x => new { x.TenantId, x.ExternalAccountId, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.ReconciliationStatus });

        builder.HasOne<ExternalAccountConnection>()
            .WithMany()
            .HasForeignKey(x => x.ExternalAccountConnectionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
