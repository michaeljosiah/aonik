using Aonik.Finance.Entities.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Loyalty;

public sealed class LoyaltyOperationConfiguration : IEntityTypeConfiguration<LoyaltyOperation>
{
    public void Configure(EntityTypeBuilder<LoyaltyOperation> builder)
    {
        builder.ToTable("AnkLoyaltyOperations", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.DetailsJson).HasMaxLength(262144).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Kind, x.SourceId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.JournalEntryLineId }).IsUnique().HasFilter("[JournalEntryLineId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.AccountId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}
