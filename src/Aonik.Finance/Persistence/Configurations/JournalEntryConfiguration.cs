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
        builder.HasIndex(x => x.Timestamp);
    }
}
