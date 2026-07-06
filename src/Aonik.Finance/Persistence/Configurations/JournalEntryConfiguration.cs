using Aonik.Finance.Entities.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LedgerId)
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.Property(x => x.SourceType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SourceId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.LedgerId);
        builder.HasIndex(x => x.SourceId);

        // #223: Timestamp is a hot ordering/range column that is always tenant-co-filtered
        // (MySpaceSummaryService period rollups; LedgerService OrderBy(Timestamp)). Lead with
        // TenantId so the composite matches the real predicate instead of a standalone scan.
        builder.HasIndex(x => new { x.TenantId, x.Timestamp });

        // One journal entry per originating business event. Manual entries all
        // share SourceId = Guid.Empty, so they are excluded from the constraint;
        // every non-manual source posts at most once per tenant. This is the
        // authority behind the idempotency check in LedgerService.
        builder.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId })
            .IsUnique()
            .HasFilter("[SourceType] <> 'Manual'");
    }
}
