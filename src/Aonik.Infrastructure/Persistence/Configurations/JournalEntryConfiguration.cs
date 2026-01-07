using Aonik.Domain.Ledger.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Infrastructure.Persistence.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountId)
            .IsRequired();

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasPrecision(19, 4);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.EntryUtc)
            .IsRequired();

        builder.Property(x => x.Reference)
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.Reference);
        builder.HasIndex(x => x.EntryUtc);
    }
}
