using Aonik.Finance.Entities.GiftCards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.GiftCards;

public sealed class GiftCardOperationConfiguration : IEntityTypeConfiguration<GiftCardOperation>
{
    public void Configure(EntityTypeBuilder<GiftCardOperation> builder)
    {
        builder.ToTable("AnkGiftCardOperations", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 4);
        builder.HasIndex(x => new { x.TenantId, x.Kind, x.SourceId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.JournalEntryLineId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.GiftCardId, x.OccurredAtUtc });
    }
}
