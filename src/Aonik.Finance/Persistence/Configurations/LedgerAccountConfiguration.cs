using Aonik.Finance.Entities.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AccountType)
            .IsRequired()
            .HasMaxLength(50);

        // #223: Code/Name are resolved on every ledger post, always with TenantId ==
        // (LedgerPostingService.ResolveRequiredAccountIdAsync). Lead each with TenantId so the
        // per-tenant chart-of-accounts lookup is a seek, not a filtered scan of a global index.
        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.Code });
        builder.HasIndex(x => x.LedgerId);
    }
}
