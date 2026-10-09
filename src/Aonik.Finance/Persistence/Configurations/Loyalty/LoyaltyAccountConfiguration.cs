using Aonik.Finance.Entities.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Loyalty;

public sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.ToTable("AnkLoyaltyAccounts", "dbo");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.PartyId }).IsUnique();
    }
}
