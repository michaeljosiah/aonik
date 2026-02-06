using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Partners.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class PartnerFundingAccountConfiguration : IEntityTypeConfiguration<PartnerFundingAccount>
{
    public void Configure(EntityTypeBuilder<PartnerFundingAccount> builder)
    {
        builder.ToTable("PartnerFundingAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId)
            .IsRequired();

        builder.Property(x => x.LedgerAccountId)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.AccountRole)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.PartnerId, x.Currency, x.AccountRole })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.LedgerAccountId })
            .IsUnique();

        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LedgerAccount>()
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
