using Aonik.Finance.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.Payments;

internal class PayoutReversalConfiguration : IEntityTypeConfiguration<PayoutReversal>
{
    public void Configure(EntityTypeBuilder<PayoutReversal> builder)
    {
        builder.ToTable("PayoutReversals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).IsRequired().HasPrecision(19, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.PayoutId });

        // JournalEntryId is a soft reference to the ledger; no FK to keep the reversal decoupled
        // from ledger lifecycle.
        builder.HasOne<Payout>().WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
    }
}
